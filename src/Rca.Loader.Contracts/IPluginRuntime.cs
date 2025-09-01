using Autodesk.Revit.UI;

namespace Rca.Loader.Contracts
{
    /// <summary>
    /// Interface for hot-reloadable plugin runtime.
    /// </summary>
    public interface IPluginRuntime
    {
        /// <summary>
        /// Gets the version of the runtime.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Initializes the runtime with the Revit application.
        /// </summary>
        /// <param name="application">The Revit UI application</param>
        void Initialize(UIControlledApplication application);

        /// <summary>
        /// Shuts down the runtime.
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Called when the runtime is loaded.
        /// </summary>
        void OnLoaded();
    }
}