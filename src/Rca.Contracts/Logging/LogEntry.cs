using System;

namespace Rca.Contracts.Logging
{
    /// <summary>
    /// Represents a single log entry in the application.
    /// </summary>
    public class LogEntry
    {
        /// <summary>
        /// Gets or sets the timestamp when the log entry was created.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the type of the log entry.
        /// </summary>
        public LogType Type { get; set; }

        /// <summary>
        /// Gets or sets the message content of the log entry.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Defines the types of log entries.
    /// </summary>
    public enum LogType
    {
        /// <summary>
        /// Informational message.
        /// </summary>
        Info,

        /// <summary>
        /// Error message.
        /// </summary>
        Error,

        /// <summary>
        /// Warning message.
        /// </summary>
        Warning,

        /// <summary>
        /// Python script output.
        /// </summary>
        PythonOutput
    }
}