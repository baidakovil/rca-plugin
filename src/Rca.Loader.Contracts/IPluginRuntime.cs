using Autodesk.Revit.UI;

namespace Rca.Loader.Contracts
{
    /// <summary>
    /// Interface for plugin runtime that can be dynamically loaded and unloaded.
    /// </summary>
    public interface IPluginRuntime
    {
        /// <summary>
        /// Gets the version of the runtime.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Initializes the runtime with Revit application context.
        /// </summary>
        /// <param name="application">The Revit UI controlled application</param>
        void Initialize(UIControlledApplication application);

        /// <summary>
        /// Shuts down the runtime and cleans up resources.
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Called after the runtime has been loaded (optional hook).
        /// </summary>
        void OnLoaded();
    }
}