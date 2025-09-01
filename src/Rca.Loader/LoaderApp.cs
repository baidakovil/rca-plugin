using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Rca.Loader.Services;
using System;
using System.Reflection;

namespace Rca.Loader
{
    /// <summary>
    /// The stable loader application that manages hot reloading of the dynamic runtime.
    /// This class is never unloaded and provides the foundation for hot reload functionality.
    /// </summary>
    public class LoaderApp : IExternalApplication
    {
        private const string RibbonTabName = "RCA Loader";
        private const string RibbonPanelName = "Hot Reload";
        private const string ReloadButtonText = "Reload Runtime";

        private RuntimeManager runtimeManager;
        private PipeServer pipeServer;

        /// <summary>
        /// Called when Revit starts up. Initializes the hot reload infrastructure.
        /// </summary>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Create and start the runtime manager
                runtimeManager = new RuntimeManager();

                // Create and start the pipe server for build system communication
                pipeServer = new PipeServer(runtimeManager);
                pipeServer.Start();

                // Create loader ribbon interface
                CreateLoaderRibbon(application);

                // Load initial runtime if available
                runtimeManager.LoadInitialRuntime(application);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Loader Error", $"Failed to initialize hot reload loader: {ex.Message}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Called when Revit shuts down. Cleans up the hot reload infrastructure.
        /// </summary>
        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                // Stop pipe server
                pipeServer?.Stop();

                // Unload current runtime
                runtimeManager?.UnloadCurrentRuntime();
                runtimeManager?.Dispose();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Loader Error", $"Failed to shutdown hot reload loader: {ex.Message}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Creates the loader ribbon interface for manual reload control.
        /// </summary>
        private void CreateLoaderRibbon(UIControlledApplication application)
        {
            try
            {
                // Create ribbon tab and panel for loader controls
                try { application.CreateRibbonTab(RibbonTabName); } catch { }
                var panel = application.CreateRibbonPanel(RibbonTabName, RibbonPanelName);

                // Create manual reload button
                var buttonData = new PushButtonData(
                    "ReloadRuntime",
                    ReloadButtonText,
                    Assembly.GetExecutingAssembly().Location,
                    typeof(Commands.ReloadRuntimeCommand).FullName);
                
                panel.AddItem(buttonData);
            }
            catch (Exception ex)
            {
                // Non-critical error - log but don't fail startup
                TaskDialog.Show("RCA Loader Warning", $"Failed to create loader ribbon: {ex.Message}");
            }
        }
    }
}