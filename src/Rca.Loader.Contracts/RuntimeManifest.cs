using System.Text.Json.Serialization;

namespace Rca.Loader.Contracts
{
    /// <summary>
    /// Manifest file structure for runtime assembly information.
    /// </summary>
    public class RuntimeManifest
    {
        /// <summary>
        /// Folder containing the runtime assembly.
        /// </summary>
        [JsonPropertyName("folder")]
        public string Folder { get; set; }

        /// <summary>
        /// Name of the runtime assembly file.
        /// </summary>
        [JsonPropertyName("assembly")]
        public string Assembly { get; set; }

        /// <summary>
        /// Timestamp when this runtime was built.
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Optional version information.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; }
    }
}