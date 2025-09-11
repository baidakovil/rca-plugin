using System;

namespace Rca.Loader.Contracts
{
    /// <summary>
    /// Interface for managing runtime assembly loading and lifecycle.
    /// </summary>
    public interface IRuntimeManager
    {
        /// <summary>
        /// Gets whether a runtime is currently loaded.
        /// </summary>
        bool IsRuntimeLoaded { get; }
        
        /// <summary>
        /// Gets the path of the currently loaded runtime, if any.
        /// </summary>
        string CurrentRuntimePath { get; }

        /// <summary>
        /// Reloads the runtime from a specified folder path.
        /// </summary>
        /// <param name="folderPath">Path to the folder containing the runtime DLL.</param>
        /// <param name="error">Error message if load fails.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool ReloadRuntime(string? folderPath, out string? error);

        /// <summary>
        /// Reloads the latest version of the runtime from the deploy root.
        /// </summary>
        /// <param name="error">Error message if operation fails.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool ReloadLatest(out string? error);

        /// <summary>
        /// Shows the standalone window from the loaded runtime.
        /// </summary>
        /// <param name="error">Error message if operation fails.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool ShowStandaloneWindow(out string? error);

        /// <summary>
        /// Unloads the current runtime, if loaded.
        /// </summary>
        void UnloadRuntime();
    }
}