using System.Text.Json.Serialization;

namespace Rca.Loader.Contracts.Protocol
{
    /// <summary>
    /// Base message envelope for pipe communication.
    /// </summary>
    public class MessageEnvelope
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
        
        [JsonPropertyName("payload")]
        public object Payload { get; set; }
    }

    /// <summary>
    /// Command message for requesting actions.
    /// </summary>
    public class CommandMessage
    {
        [JsonPropertyName("command")]
        public string Command { get; set; }
        
        [JsonPropertyName("payload")]
        public object Payload { get; set; }
    }

    /// <summary>
    /// Payload for reload command.
    /// </summary>
    public class ReloadPayload
    {
        [JsonPropertyName("folder")]
        public string Folder { get; set; }
    }

    /// <summary>
    /// Event message for status updates.
    /// </summary>
    public class EventMessage
    {
        [JsonPropertyName("event")]
        public string Event { get; set; }
        
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; }
        
        [JsonPropertyName("data")]
        public object Data { get; set; }
    }

    /// <summary>
    /// Error message for reporting failures.
    /// </summary>
    public class ErrorMessage
    {
        [JsonPropertyName("error")]
        public string Error { get; set; }
        
        [JsonPropertyName("message")]
        public string Message { get; set; }
        
        [JsonPropertyName("stackTrace")]
        public string StackTrace { get; set; }
    }

    /// <summary>
    /// Log message for debugging output.
    /// </summary>
    public class LogMessage
    {
        [JsonPropertyName("level")]
        public string Level { get; set; }
        
        [JsonPropertyName("message")]
        public string Message { get; set; }
        
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// Manifest structure for current runtime location.
    /// </summary>
    public class RuntimeManifest
    {
        [JsonPropertyName("folder")]
        public string Folder { get; set; }
        
        [JsonPropertyName("assembly")]
        public string Assembly { get; set; }
        
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; }
        
        [JsonPropertyName("version")]
        public string Version { get; set; }
    }
}