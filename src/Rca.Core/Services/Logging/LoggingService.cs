using Rca.Contracts.Logging;
using System;
using System.Collections.ObjectModel;

namespace Rca.Core.Services.Logging
{
    /// <summary>
    /// Application logging service implementation.
    /// Provides thread-safe logging capabilities with observable collection support.
    /// </summary>
    public sealed class LoggingService : ILoggingService
    {
        private readonly ObservableCollection<LogEntry> _entries = new();

        /// <summary>
        /// Gets the read-only collection of all log entries.
        /// </summary>
        public ReadOnlyObservableCollection<LogEntry> Entries { get; }

        /// <summary>
        /// Initializes a new instance of the LoggingService class.
        /// </summary>
        public LoggingService()
        {
            Entries = new ReadOnlyObservableCollection<LogEntry>(_entries);
        }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void LogInfo(string message)
        {
            AddEntry(LogType.Info, message);
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The error message to log.</param>
        public void LogError(string message)
        {
            AddEntry(LogType.Error, message);
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The warning message to log.</param>
        public void LogWarning(string message)
        {
            AddEntry(LogType.Warning, message);
        }

        /// <summary>
        /// Logs Python output.
        /// </summary>
        /// <param name="message">The Python output to log.</param>
        public void LogPythonOutput(string message)
        {
            AddEntry(LogType.PythonOutput, message);
        }

        /// <summary>
        /// Logs a custom message with specified log type.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="type">The type of the log entry.</param>
        public void LogCustom(string message, LogType type)
        {
            AddEntry(type, message);
        }

        /// <summary>
        /// Clears all log entries.
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
        }

        private void AddEntry(LogType type, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Type = type,
                Message = FormatMessage(type, message)
            };

            _entries.Add(entry);
        }

        private static string FormatMessage(LogType type, string message)
        {
            var paddedType = type.ToString().PadRight(12);
            return $"[{paddedType}] {message}";
        }
    }
}