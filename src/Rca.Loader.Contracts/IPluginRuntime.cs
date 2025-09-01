using Autodesk.Revit.UI;

namespace Rca.Loader.Contracts
{
    /// <summary>
    /// Interface for plugin runtime implementations that can be hot-reloaded.
    /// </summary>
    public interface IPluginRuntime
    {
        /// <summary>
        /// Gets the runtime version.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Initializes the runtime with the Revit UI application.
        /// </summary>
        /// <param name="application">The Revit UI application</param>
        void Initialize(UIControlledApplication application);

        /// <summary>
        /// Shuts down the runtime and cleans up resources.
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Called after the runtime has been loaded (optional hook).
        /// </summary>
        void OnLoaded() { }
    }
}