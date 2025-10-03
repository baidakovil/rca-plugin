using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using Rca.Logging.Contracts;

namespace Rca.Runtime.Logging;

/// <summary>
/// Logger provider that sends structured log entries over a dedicated named pipe to the Loader.
/// Responsible only for lifecycle and logger caching; transport/backoff handled by <see cref="PipeLogTransport"/>.
/// </summary>
public sealed class NamedPipeLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, NamedPipeLogger> _loggers = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly PipeLogTransport _transport;
    private int _disposed;

    public NamedPipeLoggerProvider(string pipeName, string runtimeSessionId, int? alcInstanceId = null)
    {
        if (string.IsNullOrWhiteSpace(pipeName)) throw new ArgumentException("Pipe name required", nameof(pipeName));
        RuntimeSessionId = runtimeSessionId ?? throw new ArgumentNullException(nameof(runtimeSessionId));
        ALCInstanceId = alcInstanceId;
        RuntimeProcessId = Environment.ProcessId;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        _transport = new PipeLogTransport(pipeName, _jsonOptions, runtimeSessionId, alcInstanceId, RuntimeProcessId);
    }

    public string RuntimeSessionId { get; }
    public int RuntimeProcessId { get; }
    public int? ALCInstanceId { get; }

    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, c => new NamedPipeLogger(c, _transport, _jsonOptions, RuntimeSessionId, RuntimeProcessId, ALCInstanceId));

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        _transport.Dispose();
        _loggers.Clear();
    }
}

/// <summary>
/// Simple transport encapsulating pipe connection, reconnection with backoff, and fallback/emergency logging.
/// </summary>
internal sealed class PipeLogTransport : IDisposable
{
    private readonly string _pipeName;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _runtimeSessionId;
    private readonly int? _alcInstanceId;
    private readonly int _runtimeProcessId;

    private NamedPipeClientStream? _pipe;
    private StreamWriter? _writer;
    private int _state; // 0=disconnected,1=connected
    private long _sequenceId;

    // backoff
    private int _backoffIndex;
    private DateTime _nextAttempt = DateTime.MinValue;
    private static readonly int[] BackoffMs = new[] { 50, 200, 500, 1000, 2000, 5000 };

    // paths
    private readonly string _baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RCA", "Logs");
    private readonly object _fileLock = new();
    private StreamWriter? _fallbackWriter;
    private DateTime _fallbackDate;
    private int _disposed;

    public PipeLogTransport(string pipeName, JsonSerializerOptions jsonOptions, string runtimeSessionId, int? alcInstanceId, int runtimeProcessId)
    {
        _pipeName = pipeName;
        _jsonOptions = jsonOptions;
        _runtimeSessionId = runtimeSessionId;
        _alcInstanceId = alcInstanceId;
        _runtimeProcessId = runtimeProcessId;
        Directory.CreateDirectory(_baseDir);
    }

    public long NextSequenceId() => Interlocked.Increment(ref _sequenceId);

    public void Write(LogEntryDto dto)
    {
        try
        {
            EnsureConnected();
            if (_state == 1 && _writer != null)
            {
                var json = JsonSerializer.Serialize(dto, _jsonOptions);
                _writer.WriteLine(json);
                _writer.Flush();
                if (_backoffIndex != 0) _backoffIndex = 0; // reset after success
                return;
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            ForceDisconnect();
        }
        catch (System.Text.Json.JsonException)
        {
            // serialization failure -> emergency file
            WriteEmergency(dto, LoggingFlags.SerializationFailed, null);
            return;
        }

        // Fallback path
        WriteFallback(dto);
    }

    private void EnsureConnected()
    {
        if (_disposed == 1) return;
        if (_state == 1 && _pipe is { IsConnected: true }) return;
        if (DateTime.UtcNow < _nextAttempt) return;

        try
        {
            _pipe?.Dispose();
            _pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            _pipe.Connect(150); // small timeout; we fallback if unavailable
            _writer = new StreamWriter(_pipe, new UTF8Encoding(false)) { AutoFlush = true };
            _state = 1;
        }
        catch (Exception)
        {
            // schedule next attempt with backoff
            int delay = BackoffMs[Math.Min(_backoffIndex, BackoffMs.Length - 1)];
            _backoffIndex = Math.Min(_backoffIndex + 1, BackoffMs.Length - 1);
            _nextAttempt = DateTime.UtcNow.AddMilliseconds(delay);
            ForceDisconnect();
        }
    }

    private void ForceDisconnect()
    {
        _state = 0;
        try { _writer?.Dispose(); } catch { }
        try { _pipe?.Dispose(); } catch { }
        _writer = null; _pipe = null;
    }

    private void WriteFallback(LogEntryDto dto)
    {
        try
        {
            var now = DateTime.Now; // local time per requirements
            if (_fallbackWriter == null || _fallbackDate.Date != now.Date)
            {
                lock (_fileLock)
                {
                    if (_fallbackWriter == null || _fallbackDate.Date != now.Date)
                    {
                        _fallbackWriter?.Dispose();
                        string path = Path.Combine(_baseDir, $"runtime-fallback-{now:yyyyMMdd}.log");
                        _fallbackWriter = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
                        _fallbackDate = now.Date;
                    }
                }
            }
            var enriched = dto with { IsFallback = true, Flags = dto.Flags | LoggingFlags.FallbackUsed };
            var json = JsonSerializer.Serialize(enriched, _jsonOptions);
            _fallbackWriter!.WriteLine(json);
        }
        catch (Exception ex)
        {
            WriteEmergency(dto, dto.Flags | LoggingFlags.FallbackUsed, ex);
        }
    }

    private void WriteEmergency(LogEntryDto dto, int flags, Exception? ex)
    {
        try
        {
            string path = Path.Combine(_baseDir, $"runtime-emergency-{DateTime.Now:yyyyMMdd}.log");
            using var sw = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
            sw.WriteLine($"EMERGENCY {DateTime.Now:O} Flags={flags} Msg={Safe(dto.Message)} Err={ex?.GetType().Name}:{ex?.Message}");
        }
        catch { /* last resort: swallow */ }
    }

    private static string Safe(string? s) => s == null ? string.Empty : s.Length > 200 ? s.Substring(0, 200) : s;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        try { _writer?.Dispose(); } catch { }
        try { _pipe?.Dispose(); } catch { }
        try { _fallbackWriter?.Dispose(); } catch { }
    }
}

/// <summary>
/// Implementation of <see cref="ILogger"/> that packages log state into a <see cref="LogEntryDto"/>.
/// </summary>
internal sealed class NamedPipeLogger : ILogger
{
    private readonly string _category;
    private readonly PipeLogTransport _transport;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _runtimeSessionId;
    private readonly int _runtimeProcessId;
    private readonly int? _alcInstanceId;

    public NamedPipeLogger(string category, PipeLogTransport transport, JsonSerializerOptions jsonOptions, string runtimeSessionId, int runtimeProcessId, int? alcInstanceId)
    {
        _category = category;
        _transport = transport;
        _jsonOptions = jsonOptions;
        _runtimeSessionId = runtimeSessionId;
        _runtimeProcessId = runtimeProcessId;
        _alcInstanceId = alcInstanceId;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        try
        {
            string message = formatter(state, exception);
            var dto = new LogEntryDto
            {
                TimestampTicks = DateTime.Now.Ticks,
                Level = logLevel.ToString(),
                Category = _category,
                Message = message,
                Exception = exception?.ToString(),
                RuntimeSessionId = _runtimeSessionId,
                SequenceId = _transport.NextSequenceId(),
                RuntimeProcessId = _runtimeProcessId,
                ALCInstanceId = _alcInstanceId,
                IsFallback = false,
                Flags = 0
            };
            _transport.Write(dto);
        }
        catch
        {
            // never throw
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
