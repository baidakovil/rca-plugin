// Placeholder classes for Linux builds where Revit API is not available
using System.Text.Json.Serialization;

namespace Autodesk.Revit.UI
{
    public class UIControlledApplication
    {
    }
}

namespace Rca.Loader.Contracts
{
    /// <summary>
    /// Interface for plugin runtime implementations that can be hot reloaded.
    /// </summary>
    public interface IPluginRuntime
    {
        /// <summary>
        /// Gets the version of the runtime.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Initializes the runtime with the provided UI application.
        /// </summary>
        /// <param name="application">The Revit UI application.</param>
        void Initialize(Autodesk.Revit.UI.UIControlledApplication application);

        /// <summary>
        /// Shuts down the runtime and cleans up resources.
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Called after the runtime has been loaded into the new assembly context.
        /// </summary>
        void OnLoaded();
    }

    /// <summary>
    /// Constants for the hot reload system.
    /// </summary>
    public static class HotReloadConstants
    {
        /// <summary>
        /// The name of the named pipe used for hot reload communication.
        /// </summary>
        public const string PipeName = "rca.hotreload";
    }

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