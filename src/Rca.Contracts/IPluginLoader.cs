using System;

namespace Rca.Contracts.Infrastructure
{
    /// <summary>
    /// Interface for plugin loading and unloading capabilities.
    /// </summary>
    public interface IPluginLoader
    {
        /// <summary>
        /// Loads the plugin from the specified assembly path.
        /// </summary>
        /// <param name="assemblyPath">Path to the plugin assembly.</param>
        /// <returns>True if loaded successfully, false otherwise.</returns>
        bool LoadPlugin(string assemblyPath);

        /// <summary>
        /// Unloads the currently loaded plugin.
        /// </summary>
        /// <returns>True if unloaded successfully, false otherwise.</returns>
        bool UnloadPlugin();

        /// <summary>
        /// Reloads the plugin (unload then load).
        /// </summary>
        /// <param name="assemblyPath">Path to the plugin assembly.</param>
        /// <returns>True if reloaded successfully, false otherwise.</returns>
        bool ReloadPlugin(string assemblyPath);

        /// <summary>
        /// Gets whether a plugin is currently loaded.
        /// </summary>
        bool IsPluginLoaded { get; }

        /// <summary>
        /// Event raised when plugin loading fails.
        /// </summary>
        event EventHandler<string> LoadingFailed;
    }
}