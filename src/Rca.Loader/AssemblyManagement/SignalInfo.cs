using System;

namespace Rca.Loader.AssemblyManagement
{
    /// <summary>
    /// Represents signal information from MSBuild, including timestamp and event type.
    /// </summary>
    /// <remarks>
    /// This class tracks the last build notification and its effect on assemblies.
    /// </remarks>
    public class SignalInfo
    {
        /// <summary>
        /// Gets or sets the time of the last MSBuild signal in HH:MM:SS format.
        /// </summary>
        public string Time { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the full timestamp (ISO 8601) of the last MSBuild signal.
        /// </summary>
        /// <remarks>
        /// This value is intended for programmatic checks in tests and logs where
        /// a stable, monotonic timestamp is required. The user-facing `Time`
        /// property remains in HH:mm:ss format for display in the UI.
        /// </remarks>
        public string Timestamp { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the event type from the last MSBuild signal.
        /// </summary>
        /// <remarks>
        /// Possible values:
        /// - "no changes" - No assemblies were changed
        /// - "only runtime outdated" - Only the runtime assembly was changed
        /// - "only loader outdated" - Only loader/contracts assemblies were changed
        /// - "both loader and runtime outdated" - Both runtime and loader were changed
        /// </remarks>
        public string Event { get; set; } = "no changes";
    }
}
