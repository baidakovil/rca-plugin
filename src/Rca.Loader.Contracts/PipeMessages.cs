using System;

namespace Rca.Loader.Contracts
{
    /// <summary>
    /// Base class for all pipe messages.
    /// </summary>
    public abstract class PipeMessage
    {
        /// <summary>
        /// Gets the type of the message.
        /// </summary>
        public abstract string Type { get; }

        /// <summary>
        /// Gets the timestamp when the message was created.
        /// </summary>
        public DateTime Timestamp { get; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Command message for pipe communication.
    /// </summary>
    public class CommandMessage : PipeMessage
    {
        public override string Type => "COMMAND";

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
        public override string Type => "EVENT";

        /// <summary>
        /// Gets or sets the event name.
        /// </summary>
        public string Event { get; set; }

        /// <summary>
        /// Gets or sets the event payload.
        /// </summary>
        public object Payload { get; set; }
    }

    /// <summary>
    /// Payload for reload commands.
    /// </summary>
    public class ReloadPayload
    {
        /// <summary>
        /// Gets or sets the folder containing the runtime assembly.
        /// </summary>
        public string Folder { get; set; }

        /// <summary>
        /// Gets or sets the name of the runtime assembly file.
        /// </summary>
        public string AssemblyName { get; set; } = "Rca.Dynamic.dll";
    }

    /// <summary>
    /// Payload for error messages.
    /// </summary>
    public class ErrorPayload
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
    /// Payload for log messages.
    /// </summary>
    public class LogPayload
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
        /// Gets or sets the logger name.
        /// </summary>
        public string Logger { get; set; }
    }
}