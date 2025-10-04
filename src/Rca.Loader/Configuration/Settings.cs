namespace Rca.Loader.Configuration
{
    /// <summary>
    /// Application settings loaded from settings.json.
    /// Contains both release and debug-only settings.
    /// </summary>
    public class Settings
    {
        /// <summary>
        /// Gets or sets whether to automatically load the runtime on startup.
        /// Default: true
        /// </summary>
        public bool AutoLoadRuntimeOnStartup { get; set; } = true;

#if DEBUG
        /// <summary>
        /// Debug-only settings (only available in DEBUG builds).
        /// </summary>
        public DebugSettings Debug { get; set; } = new DebugSettings();
#endif
    }

#if DEBUG
    /// <summary>
    /// Debug-specific settings (only available in DEBUG builds).
    /// </summary>
    public class DebugSettings
    {
        /// <summary>
        /// Gets or sets whether to show verbose logging in debug mode.
        /// Default: true
        /// </summary>
        public bool VerboseLogging { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to auto-show the dockable panel on runtime load.
        /// Default: false
        /// </summary>
        public bool AutoShowPanelOnLoad { get; set; } = false;
    }
#endif
}
