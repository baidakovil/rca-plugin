using System.Collections.ObjectModel;

namespace Rca.Contracts.Logging
{
    /// <summary>
    /// Interface for the application logging service.
    /// </summary>
    public interface ILoggingService
    {
        /// <summary>
        /// Gets the collection of all log entries.
        /// </summary>
        ReadOnlyObservableCollection<LogEntry> Entries { get; }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void LogInfo(string message);

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The error message to log.</param>
        void LogError(string message);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The warning message to log.</param>
        void LogWarning(string message);

        /// <summary>
        /// Logs Python output.
        /// </summary>
        /// <param name="message">The Python output to log.</param>
        void LogPythonOutput(string message);

        /// <summary>
        /// Logs a custom message with specified log type.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="type">The type of the log entry.</param>
        void LogCustom(string message, LogType type);

        /// <summary>
        /// Clears all log entries.
        /// </summary>
        void Clear();
    }
}