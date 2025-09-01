using Autodesk.Revit.UI;

namespace Rca.Loader.Contracts
{
    /// <summary>
    /// Interface for runtime plugin implementations that can be hot-reloaded.
    /// </summary>
    public interface IPluginRuntime
    {
        /// <summary>
        /// Gets the version of the runtime implementation.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Initializes the runtime with the Revit UI application.
        /// </summary>
        /// <param name="application">The UIControlledApplication from Revit</param>
        void Initialize(UIControlledApplication application);

        /// <summary>
        /// Shuts down the runtime and cleans up resources.
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Called after the runtime has been loaded into the new AssemblyLoadContext.
        /// </summary>
        void OnLoaded();
    }
}