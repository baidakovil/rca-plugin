using System.Text.Json.Serialization;

namespace Rca.Loader.Contracts
{
    /// <summary>
    /// Base class for all pipe messages.
    /// </summary>
    public abstract class PipeMessage
    {
        /// <summary>
        /// Gets or sets the message type.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    /// <summary>
    /// Represents a command message sent to the loader.
    /// </summary>
    public class CommandMessage : PipeMessage
    {
        /// <summary>
        /// Gets or sets the command payload.
        /// </summary>
        [JsonPropertyName("payload")]
        public object Payload { get; set; }
    }

    /// <summary>
    /// Represents an event message sent from the loader.
    /// </summary>
    public class EventMessage : PipeMessage
    {
        /// <summary>
        /// Gets or sets the event payload.
        /// </summary>
        [JsonPropertyName("payload")]
        public object Payload { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the event.
        /// </summary>
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// Payload for the RELOAD command.
    /// </summary>
    public class ReloadPayload
    {
        /// <summary>
        /// Gets or sets the folder containing the runtime assembly.
        /// </summary>
        [JsonPropertyName("folder")]
        public string Folder { get; set; }
    }

    /// <summary>
    /// Payload for error messages.
    /// </summary>
    public class ErrorPayload
    {
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the error details.
        /// </summary>
        [JsonPropertyName("details")]
        public string Details { get; set; }
    }

    /// <summary>
    /// Payload for log messages.
    /// </summary>
    public class LogPayload
    {
        /// <summary>
        /// Gets or sets the log level.
        /// </summary>
        [JsonPropertyName("level")]
        public string Level { get; set; }

        /// <summary>
        /// Gets or sets the log message.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }

    /// <summary>
    /// Message types for pipe communication.
    /// </summary>
    public static class MessageTypes
    {
        // Commands (client to server)
        public const string Reload = "RELOAD";

        // Events (server to client)
        public const string ReloadAccepted = "RELOAD_ACCEPTED";
        public const string ReloadStart = "RELOAD_START";
        public const string ReloadDone = "RELOAD_DONE";
        public const string ReloadFail = "RELOAD_FAIL";
        public const string RuntimeError = "RUNTIME_ERROR";
        public const string Log = "LOG";

        // Debug events
        public const string AlcCollected = "ALC_COLLECTED";
    }
}