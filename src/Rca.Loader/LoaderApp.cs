using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace Rca.Loader
{
    /// <summary>
    /// The main loader application for the RCA Plugin hot reload system.
    /// This application remains stable and loads/unloads the dynamic runtime.
    /// </summary>
    public class LoaderApp : IExternalApplication
    {
        private const string LoaderTabName = "RCA Loader";
        private const string LoaderPanelName = "Hot Reload";

        private RuntimeManager runtimeManager;
        private PipeServer pipeServer;

        // Static reference for commands to access the runtime manager
        private static RuntimeManager staticRuntimeManager;

        /// <summary>
        /// Gets the runtime manager instance for command access.
        /// </summary>
        /// <returns>The runtime manager instance or null if not initialized</returns>
        public static RuntimeManager GetRuntimeManager()
        {
            return staticRuntimeManager;
        }

        /// <summary>
        /// Called when Revit starts up.
        /// </summary>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Initialize the runtime manager
                runtimeManager = new RuntimeManager();
                staticRuntimeManager = runtimeManager;

                // Start the pipe server for hot reload communication
                pipeServer = new PipeServer(runtimeManager);
                pipeServer.Start();

                // Create a simple loader ribbon for manual reload
                CreateLoaderRibbon(application);

                // Load the initial runtime
                runtimeManager.LoadRuntime(application);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Loader Error", $"Failed to initialize loader: {ex.Message}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Called when Revit shuts down.
        /// </summary>
        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                // Stop the pipe server
                pipeServer?.Stop();

                // Unload the runtime
                runtimeManager?.UnloadRuntime();

                // Clear static reference
                staticRuntimeManager = null;

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Loader Error", $"Error during shutdown: {ex.Message}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Creates the loader ribbon with manual reload command.
        /// </summary>
        private void CreateLoaderRibbon(UIControlledApplication application)
        {
            try
            {
                application.CreateRibbonTab(LoaderTabName);
            }
            catch
            {
                // Tab already exists
            }

            var panel = application.CreateRibbonPanel(LoaderTabName, LoaderPanelName);

            var reloadButtonData = new PushButtonData(
                "ReloadRuntime",
                "Reload Runtime",
                System.Reflection.Assembly.GetExecutingAssembly().Location,
                typeof(Commands.ReloadRuntimeCommand).FullName);

            reloadButtonData.ToolTip = "Manually reload the RCA runtime";
            panel.AddItem(reloadButtonData);
        }
    }
}