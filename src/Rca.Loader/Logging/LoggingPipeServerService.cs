using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Rca.Logging.Contracts;

namespace Rca.Loader.Logging;

public sealed class LoggingPipeServerService : IDisposable
{
    private readonly string _pipeName;
    private readonly CancellationTokenSource _cts = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private int _started;
    private long _globalSeq;
    private Task? _loop;
    private readonly LogDispatcher _dispatcher;

    public LoggingPipeServerService(string pipeName)
    {
        _pipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            AllowTrailingCommas = true
        };
        _dispatcher = new LogDispatcher();
    }

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1) return;
        _loop = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        var ct = _cts.Token;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                Debug.WriteLine("[LoggingPipe] Waiting for runtime logging connection...");
                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
                Debug.WriteLine("[LoggingPipe] Runtime connected for logging");
                using var reader = new StreamReader(server);
                while (!ct.IsCancellationRequested && server.IsConnected)
                {
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line == null) break;
                    ProcessLine(line);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoggingPipe] Error: {ex.Message}");
                await Task.Delay(500, ct).ConfigureAwait(false);
            }
        }
    }

    private void ProcessLine(string line)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<LogEntryDto>(line, _jsonOptions);
            if (dto == null) return;
            if (dto.SchemaVersion != LoggingSchema.Version)
            {
                // incompatible now -> skip (future: write special file)
                return;
            }
            if (dto.IsPing) return; // suppress keepalive pings
            var enriched = new EnrichedLogEntry(dto)
            {
                GlobalSequenceId = Interlocked.Increment(ref _globalSeq),
                ReceivedTimestamp = DateTime.Now,
                LoaderProcessId = Environment.ProcessId
            };
            _dispatcher.Dispatch(enriched);
        }
        catch (JsonException jex)
        {
            Debug.WriteLine($"[LoggingPipe] JSON parse failed: {jex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LoggingPipe] Processing error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _loop?.Wait(500); } catch { }
        _cts.Dispose();
        _dispatcher.Dispose();
    }
}

public sealed class EnrichedLogEntry
{
    public EnrichedLogEntry(LogEntryDto dto) => Dto = dto;
    public LogEntryDto Dto { get; }
    public long GlobalSequenceId { get; set; }
    public DateTime ReceivedTimestamp { get; set; }
    public int LoaderProcessId { get; set; }
}

internal sealed class LogDispatcher : IDisposable
{
    private readonly FileLogSink _fileSink = new();
    private readonly DebugSink _debugSink = new();

    public void Dispatch(EnrichedLogEntry entry)
    {
        _fileSink.Write(entry);
        _debugSink.Write(entry);
    }

    public void Dispose()
    {
        _fileSink.Dispose();
    }
}

internal sealed class FileLogSink : IDisposable
{
    private readonly string _dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RCA", "Logs");
    private readonly StreamWriter _writer;

    public FileLogSink()
    {
        Directory.CreateDirectory(_dir);
        string path = Path.Combine(_dir, $"rca-logs-{DateTime.Now:yyyyMMdd_HHmmss}.log");
        _writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
        _writer.WriteLine($"# RCA Loader Log Session started {DateTime.Now:O}");
    }

    public void Write(EnrichedLogEntry e)
    {
        try
        {
            var d = e.Dto;
            _writer.WriteLine($"{e.GlobalSequenceId}|{new DateTime(d.TimestampTicks):O}|Recv:{e.ReceivedTimestamp:O}|{d.Level}|{d.Category}|{Escape(d.Message)}|F={d.Flags}|Seq={d.SequenceId}|Proc={d.RuntimeProcessId}|Sess={d.RuntimeSessionId}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FileLogSink] Write failed: {ex.Message}");
        }
    }

    private static string Escape(string s) => s.Replace('\n', ' ').Replace('\r', ' ');

    public void Dispose()
    {
        try { _writer.Dispose(); } catch { }
    }
}

internal sealed class DebugSink
{
    public void Write(EnrichedLogEntry e)
    {
        var d = e.Dto;
        Debug.WriteLine($"[RCA]{d.Level}:{d.Category} {d.Message}");
    }
}
