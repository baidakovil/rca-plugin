using System.Text.Json.Serialization;

namespace Rca.Loader.Contracts.Protocol
{
    /// <summary>
    /// Base class for all pipe communication messages.
    /// </summary>
    public abstract class MessageBase
    {
        /// <summary>
        /// The message type identifier.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    /// <summary>
    /// Command message sent to the loader.
    /// </summary>
    public class CommandMessage : MessageBase
    {
        /// <summary>
        /// Command payload data.
        /// </summary>
        [JsonPropertyName("payload")]
        public object Payload { get; set; }
    }

    /// <summary>
    /// Event message sent from the loader.
    /// </summary>
    public class EventMessage : MessageBase
    {
        /// <summary>
        /// Event payload data.
        /// </summary>
        [JsonPropertyName("payload")]
        public object Payload { get; set; }

        /// <summary>
        /// Timestamp of the event.
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Payload for reload command.
    /// </summary>
    public class ReloadPayload
    {
        /// <summary>
        /// Optional folder path override for the runtime to load.
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
        /// Error message.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; }

        /// <summary>
        /// Optional exception details.
        /// </summary>
        [JsonPropertyName("exception")]
        public string Exception { get; set; }
    }

    /// <summary>
    /// Payload for log messages.
    /// </summary>
    public class LogPayload
    {
        /// <summary>
        /// Log level (Debug, Info, Warning, Error).
        /// </summary>
        [JsonPropertyName("level")]
        public string Level { get; set; }

        /// <summary>
        /// Log message.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }

    /// <summary>
    /// Message types for pipe communication.
    /// </summary>
    public static class MessageTypes
    {
        // Commands (inbound to loader)
        public const string Reload = "RELOAD";

        // Events (outbound from loader)
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