using System;

namespace Rca.Loader.Contracts
{
    /// <summary>
    /// Constants for named pipe communication.
    /// </summary>
    public static class PipeConstants
    {
        /// <summary>
        /// Named pipe name for hot reload communication.
        /// </summary>
        public const string PipeName = "rca.hotreload";
    }

    /// <summary>
    /// Base message envelope for pipe communication.
    /// </summary>
    public class PipeMessage
    {
        /// <summary>
        /// Gets or sets the message type.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the message timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Command message for pipe communication.
    /// </summary>
    public class CommandMessage : PipeMessage
    {
        /// <summary>
        /// Gets or sets the command name.
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// Gets or sets the command payload.
        /// </summary>
        public object Payload { get; set; }
    }

    /// <summary>
    /// Event message for pipe communication.
    /// </summary>
    public class EventMessage : PipeMessage
    {
        /// <summary>
        /// Gets or sets the event name.
        /// </summary>
        public string Event { get; set; }

        /// <summary>
        /// Gets or sets the event data.
        /// </summary>
        public object Data { get; set; }
    }

    /// <summary>
    /// Reload command payload.
    /// </summary>
    public class ReloadPayload
    {
        /// <summary>
        /// Gets or sets the folder path containing the new runtime assembly.
        /// </summary>
        public string Folder { get; set; }

        /// <summary>
        /// Gets or sets whether to force reload even if no changes detected.
        /// </summary>
        public bool Force { get; set; }
    }

    /// <summary>
    /// Error message for pipe communication.
    /// </summary>
    public class ErrorMessage : PipeMessage
    {
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the exception details.
        /// </summary>
        public string Exception { get; set; }
    }

    /// <summary>
    /// Log message for pipe communication.
    /// </summary>
    public class LogMessage : PipeMessage
    {
        /// <summary>
        /// Gets or sets the log level.
        /// </summary>
        public string Level { get; set; }

        /// <summary>
        /// Gets or sets the log message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the source component.
        /// </summary>
        public string Source { get; set; }
    }
}