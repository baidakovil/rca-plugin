using System;

namespace Rca.Loader.Contracts
{
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
        /// Gets or sets the message payload as JSON string.
        /// </summary>
        public string Payload { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the message.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Command message for pipe communication.
    /// </summary>
    public class CommandMessage : PipeMessage
    {
        /// <summary>
        /// Gets or sets the command to execute.
        /// </summary>
        public string Command { get; set; }
    }

    /// <summary>
    /// Reload command payload.
    /// </summary>
    public class ReloadPayload
    {
        /// <summary>
        /// Gets or sets the folder path containing the new runtime.
        /// </summary>
        public string Folder { get; set; }

        /// <summary>
        /// Gets or sets whether to force reload even if version is the same.
        /// </summary>
        public bool Force { get; set; }
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
    }

    /// <summary>
    /// Error message for pipe communication.
    /// </summary>
    public class ErrorMessage : PipeMessage
    {
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Gets or sets the stack trace.
        /// </summary>
        public string StackTrace { get; set; }
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
    }

    /// <summary>
    /// Runtime manifest for tracking current loaded assembly.
    /// </summary>
    public class RuntimeManifest
    {
        /// <summary>
        /// Gets or sets the folder containing the runtime assembly.
        /// </summary>
        public string Folder { get; set; }

        /// <summary>
        /// Gets or sets the runtime assembly file name.
        /// </summary>
        public string Assembly { get; set; }

        /// <summary>
        /// Gets or sets the version of the runtime.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the runtime was built.
        /// </summary>
        public DateTime BuildTime { get; set; }
    }
}