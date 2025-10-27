using System;
using System.IO;
using System.Text.Json;
using Rca.Loader.Logging;
using Rca.Loader.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Rca.Loader.Configuration
{
    /// <summary>
    /// Service for loading and managing application settings from settings.json.
    /// Provides fallback to default values if file is missing or corrupted.
    /// </summary>
    public class SettingsService
    {
        private static readonly ILogger Log = LoaderLog.GetLogger<SettingsService>();
        private static Settings? cachedSettings;
        private static readonly object lockObject = new object();

        /// <summary>
        /// Gets the settings file path.
        /// Path: %ProgramData%\Autodesk\Revit\Addins\{RevitVersion}\Revit Chat Assistant\settings.json
        /// </summary>
        public static string SettingsFilePath
        {
            get
            {
                var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                // Use centralized RevitVersion property
                var revitVersion = LoaderConstants.RevitVersion;
                return Path.Combine(
                    programData,
                    "Autodesk",
                    "Revit",
                    "Addins",
                    revitVersion,
                    "Revit Chat Assistant",
                    "settings.json"
                );
            }
        }

        /// <summary>
        /// Loads settings from settings.json file.
        /// Returns default settings if file doesn't exist or cannot be read.
        /// Thread-safe with caching.
        /// </summary>
        /// <returns>Settings instance (never null).</returns>
        public static Settings LoadSettings()
        {
            lock (lockObject)
            {
                // Return cached settings if already loaded
                if (cachedSettings != null)
                {
                    return cachedSettings;
                }

                var settingsPath = SettingsFilePath;
                
                // If file doesn't exist, use defaults
                if (!File.Exists(settingsPath))
                {
                    Log.LogInformation("Settings file not found at {Path}, using defaults", settingsPath);
                    cachedSettings = new Settings();
                    return cachedSettings;
                }

                try
                {
                    var json = File.ReadAllText(settingsPath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    };

                    var settings = JsonSerializer.Deserialize<Settings>(json, options);
                    
                    if (settings == null)
                    {
                        Log.LogWarning("Failed to deserialize settings from {Path}, using defaults", settingsPath);
                        cachedSettings = new Settings();
                        return cachedSettings;
                    }

                    cachedSettings = settings;
                    Log.LogInformation("Settings loaded from {Path}", settingsPath);
                    Log.LogDebug("AutoLoadRuntimeOnStartup={AutoLoad}", settings.AutoLoadRuntimeOnStartup);
#if DEBUG
                    Log.LogDebug("Debug.VerboseLogging={Verbose}, Debug.AutoShowPanelOnLoad={AutoShow}", 
                        settings.Debug.VerboseLogging, settings.Debug.AutoShowPanelOnLoad);
#endif
                    return cachedSettings;
                }
                catch (Exception ex)
                {
                    Log.LogError(ex, "Error loading settings from {Path}, using defaults", settingsPath);
                    cachedSettings = new Settings();
                    return cachedSettings;
                }
            }
        }

        /// <summary>
        /// Clears the cached settings, forcing a reload on next access.
        /// Useful for testing or when settings file is updated externally.
        /// </summary>
        public static void ClearCache()
        {
            lock (lockObject)
            {
                cachedSettings = null;
                Log.LogDebug("Settings cache cleared");
            }
        }
    }
}
