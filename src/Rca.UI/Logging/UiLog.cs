using System;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text.Json;
using System.Text;
using System.IO;
using System.Threading;

namespace Rca.UI.Logging
{
    /// <summary>
    /// Lightweight UI-side logging adapter that mirrors runtime logger behavior without introducing
    /// a hard compile-time dependency on runtime logging transport types. Used only to eliminate
    /// residual Debug.WriteLine calls inside UI project so that all diagnostic output funnels through
    /// the unified named pipe if available, falling back silently otherwise.
    /// 
    /// NOTE: This is a standalone implementation that does NOT depend on Rca.Logging.Contracts.
    /// It uses an internal DTO structure that matches the contract for serialization.
    /// </summary>
    internal static class UiLog
    {
        private static readonly ConcurrentDictionary<string, ILogger> _cache = new();
        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        private static readonly string SessionId = "UI-" + Guid.NewGuid().ToString("N");
        private static long _seq;
        private static volatile PipeState _state = PipeState.Disconnected;
        private static DateTime _nextAttempt = DateTime.MinValue;
        private static readonly object _connectLock = new();
        private static StreamWriter? _writer;
        private static int _backoffIndex;
        private static readonly int[] Backoff = { 50, 200, 500, 1000, 2000, 5000 };
        private static readonly Random _rng = new();
        private const string PipeName = "RCA_LOG_PIPE";
        private static readonly string BaseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RCA", "Logs");

        private enum PipeState { Disconnected, Connected }

        public static ILogger GetLogger<T>() => GetLogger(typeof(T).FullName ?? typeof(T).Name);
        public static ILogger GetLogger(string category) => _cache.GetOrAdd(category, c => new UiPipeLogger(c));

        private static void EnsureConnected()
        {
            if (_state == PipeState.Connected && _writer != null) return;
            if (DateTime.UtcNow < _nextAttempt) return;
            lock (_connectLock)
            {
                if (_state == PipeState.Connected && _writer != null) return;
                try
                {
                    var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous);
                    pipe.Connect(120);
                    _writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true };
                    _state = PipeState.Connected;
                    _backoffIndex = 0;
                }
                catch
                {
                    _state = PipeState.Disconnected;
                    int baseDelay = Backoff[Math.Min(_backoffIndex, Backoff.Length - 1)];
                    _backoffIndex = Math.Min(_backoffIndex + 1, Backoff.Length - 1);
                    double jitter = 0.8 + _rng.NextDouble() * 0.4;
                    _nextAttempt = DateTime.UtcNow.AddMilliseconds(baseDelay * jitter);
                }
            }
        }

        private sealed class UiPipeLogger : ILogger
        {
            private readonly string _category;
            public UiPipeLogger(string category) => _category = category;
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;
                try
                {
                    var msg = formatter(state, exception);
                    
                    // Create inline DTO that matches Rca.Logging.Contracts.LogEntryDto structure
                    // This avoids compile-time dependency on Logging.Contracts
                    var dto = new
                    {
                        SchemaVersion = "1",
                        TimestampTicks = DateTime.Now.Ticks,
                        Level = logLevel.ToString(),
                        Category = _category,
                        Message = msg,
                        Exception = exception?.ToString(),
                        RuntimeSessionId = SessionId,
                        SequenceId = Interlocked.Increment(ref _seq),
                        RuntimeProcessId = Environment.ProcessId,
                        IsFallback = false,
                        Flags = 0,
                        IsPing = false
                    };
                    
                    EnsureConnected();
                    if (_state == PipeState.Connected && _writer != null)
                    {
                        try
                        {
                            var json = JsonSerializer.Serialize(dto, _json);
                            _writer.WriteLine(json);
                            return;
                        }
                        catch
                        {
                            // force disconnect and fallback
                            try { _writer?.Dispose(); } catch { }
                            _state = PipeState.Disconnected;
                        }
                    }
                    // minimal fallback (UI rarely critical): append plain text
                    try
                    {
                        Directory.CreateDirectory(BaseDir);
                        var path = Path.Combine(BaseDir, $"ui-fallback-{DateTime.Now:yyyyMMdd}.log");
                        File.AppendAllText(path, $"{DateTime.Now:O}|{logLevel}|{_category}|{msg}{Environment.NewLine}");
                    }
                    catch { }
                }
                catch { }
            }
        }

        private sealed class NullScope : IDisposable { public static readonly NullScope Instance = new(); public void Dispose() { } }
    }
}
