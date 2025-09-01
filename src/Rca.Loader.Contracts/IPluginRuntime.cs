using Autodesk.Revit.UI;

namespace Rca.Loader.Contracts
{
    /// <summary>
    /// Interface for hot-reloadable plugin runtime implementations.
    /// </summary>
    public interface IPluginRuntime
    {
        /// <summary>
        /// Gets the version of the runtime implementation.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Initializes the plugin runtime with the Revit UI application.
        /// </summary>
        /// <param name="application">The UIControlledApplication from Revit</param>
        void Initialize(UIControlledApplication application);

        /// <summary>
        /// Shuts down the plugin runtime and cleans up resources.
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Called after the runtime has been loaded into the new AssemblyLoadContext.
        /// </summary>
        void OnLoaded();
    }
}