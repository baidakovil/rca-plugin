using System.Collections.ObjectModel;

namespace Rca.Contracts
{
    /// <summary>
    /// Interface for debug logging service.
    /// </summary>
    public interface IDebugLogService
    {
        /// <summary>
        /// Gets the read-only collection of debug log entries.
        /// </summary>
        ReadOnlyObservableCollection<IDebugLogEntry> Entries { get; }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log</param>
        void LogInfo(string message);

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The error message to log</param>
        void LogError(string message);

        /// <summary>
        /// Logs Python output.
        /// </summary>
        /// <param name="message">The Python output to log</param>
        void LogPythonOutput(string message);

        /// <summary>
        /// Logs a custom message with a specific log type.
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="type">The log type</param>
        void LogCustom(string message, DebugLogType type);
    }

    /// <summary>
    /// Interface for debug log entry.
    /// </summary>
    public interface IDebugLogEntry
    {
        /// <summary>
        /// Gets the timestamp of the log entry.
        /// </summary>
        System.DateTime Timestamp { get; }

        /// <summary>
        /// Gets the log type.
        /// </summary>
        DebugLogType Type { get; }

        /// <summary>
        /// Gets the log message.
        /// </summary>
        string Message { get; }
    }

    /// <summary>
    /// Enumeration of debug log types.
    /// </summary>
    public enum DebugLogType
    {
        Info,
        Error,
        PythonOutput
    }
}