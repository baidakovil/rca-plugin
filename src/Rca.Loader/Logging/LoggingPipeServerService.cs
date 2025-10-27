using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Rca.Generated;
using Rca.Logging.Contracts;
using Microsoft.Extensions.Logging;

namespace Rca.Loader.Logging;

/// <summary>
/// Persistent named pipe server for receiving runtime log entries as JSONL.
/// Provides sinks also used for internal loader logging (see LoaderLog).
/// </summary>
public sealed class LoggingPipeServerService : IDisposable
{
    private readonly string _pipeName;
    private readonly CancellationTokenSource _cts = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private int _started;
    private long _globalSeq;
    private Task? _loop;

    public LoggingPipeServerService(string pipeName)
    {
        _pipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
        _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, AllowTrailingCommas = true };
        LoaderLog.EnsureDispatcher();
    }

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1) return;
        LoaderLog.InternalLogger?.LogInformation("Logging pipe server starting on {Pipe}", _pipeName);
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
                LoaderLog.InternalLogger?.LogInformation("[Pipe] Waiting for runtime logging connection on {Pipe}", _pipeName);
                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
                LoaderLog.InternalLogger?.LogInformation("[Pipe] Runtime connected for logging");
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
                LoaderLog.InternalLogger?.LogError(ex, "[Pipe] Error in logging server loop");
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
            if (dto.SchemaVersion != LoggingSchema.Version) return; // skip incompatible
            if (dto.IsPing) return; // suppress pings
            var enriched = new EnrichedLogEntry(dto)
            {
                GlobalSequenceId = Interlocked.Increment(ref _globalSeq),
                ReceivedTimestamp = DateTime.Now,
                LoaderProcessId = Environment.ProcessId
            };
            LoaderLog.Dispatch(enriched);
        }
        catch (JsonException jex)
        {
            LoaderLog.InternalLogger?.LogDebug(jex, "[Pipe] JSON parse failed (len={Len})", line.Length);
        }
        catch (Exception ex)
        {
            LoaderLog.InternalLogger?.LogError(ex, "[Pipe] Processing error");
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _loop?.Wait(500); } catch { }
        _cts.Dispose();
        LoaderLog.InternalLogger?.LogInformation("Logging pipe server stopped");
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

internal static class LoaderLog
{
    private static FileLogSink? _fileSink;
    private static DebugSink? _debugSink;
    private static ILogger? _internal;

    public static ILogger? InternalLogger => _internal;

    public static void EnsureDispatcher()
    {
        if (_fileSink != null) return;
        _fileSink = new FileLogSink();
        _debugSink = new DebugSink();
        _internal = new LoaderInternalLogger("Rca.Loader");
    }

    public static void Dispatch(EnrichedLogEntry entry)
    {
        _fileSink?.Write(entry);
        _debugSink?.Write(entry);
    }

    public static ILogger GetLogger<T>() => new LoaderInternalLogger(typeof(T).FullName ?? "Loader");

    private sealed class LoaderInternalLogger : ILogger
    {
        private readonly string _category;
        public LoaderInternalLogger(string category) => _category = category;
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var msg = formatter(state, exception);
            _fileSink?.WriteLoaderInternal(_category, logLevel, msg, exception);
            _debugSink?.WriteLoaderInternal(_category, logLevel, msg, exception);
        }
        private sealed class NullScope : IDisposable { public static readonly NullScope Instance = new(); public void Dispose() { } }
    }
}

internal sealed class FileLogSink : IDisposable
{
    private readonly string _dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RCA", "Logs");
    private readonly StreamWriter _w;

    public FileLogSink()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, $"rca-logs-{DateTime.Now.ToString(RcaBuildMetadata.TimestampPattern)}.log");
        _w = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
        _w.WriteLine($"# RCA Loader Log Session started {DateTime.Now:O}");
    }

    public void Write(EnrichedLogEntry e)
    {
        try
        {
            var d = e.Dto;
            _w.WriteLine($"{e.GlobalSequenceId}|{new DateTime(d.TimestampTicks):O}|Recv:{e.ReceivedTimestamp:O}|{d.Level}|{d.Category}|{Escape(d.Message)}|F={d.Flags}|Seq={d.SequenceId}|Proc={d.RuntimeProcessId}|Sess={d.RuntimeSessionId}");
        }
        catch { }
    }

    public void WriteLoaderInternal(string category, LogLevel level, string message, Exception? ex)
    {
        try
        {
            _w.WriteLine($"LOADER|{DateTime.Now:O}|{level}|{category}|{Escape(message)}{(ex!=null?"|EX="+Escape(ex.GetType().Name+":"+ex.Message):string.Empty)}");
        }
        catch { }
    }

    private static string Escape(string s) => s.Replace('\n', ' ').Replace('\r', ' ');
    public void Dispose() { try { _w.Dispose(); } catch { } }
}

internal sealed class DebugSink
{
    public void Write(EnrichedLogEntry e)
    {
        var d = e.Dto;
        System.Diagnostics.Debug.WriteLine($"[RCA]{d.Level}:{d.Category} {d.Message}");
    }
    public void WriteLoaderInternal(string category, LogLevel level, string message, Exception? ex)
    {
        System.Diagnostics.Debug.WriteLine($"[RCA][Loader]{level}:{category} {message}{(ex!=null?" EX="+ex.Message:string.Empty)}");
    }
}
