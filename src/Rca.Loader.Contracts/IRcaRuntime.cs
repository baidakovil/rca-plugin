using Autodesk.Revit.UI;

namespace Rca.Loader.Contracts
{
    /// <summary>
    /// Interface for RCA runtime operations, allowing decoupled loader/runtime interaction.
    /// </summary>
    public interface IRcaRuntime
    {
        /// <summary>
        /// Gets the version of the runtime assembly.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Initializes and starts the RCA runtime.
        /// </summary>
        /// <param name="application">The Revit UI application instance</param>
        /// <returns>True if startup succeeded, false otherwise</returns>
        bool Startup(UIControlledApplication application);

        /// <summary>
        /// Shuts down the RCA runtime and performs cleanup.
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Executes a test method by name (for future test runner integration).
        /// </summary>
        /// <param name="testFilter">Test filter (e.g., "Namespace.Class.TestMethod")</param>
        /// <returns>Test result information</returns>
        string RunTest(string testFilter);

        /// <summary>
        /// Gets runtime status information for diagnostics.
        /// </summary>
        /// <returns>Status information as formatted string</returns>
        string GetStatus();
    }
}