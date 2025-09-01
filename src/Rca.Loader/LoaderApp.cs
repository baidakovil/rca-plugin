using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Rca.Loader.Contracts;
using System;
using System.Reflection;

namespace Rca.Loader
{
    /// <summary>
    /// The stable loader application for hot reload functionality.
    /// </summary>
    public class LoaderApp : IExternalApplication
    {
        private const string RibbonTabName = "RCA Loader";
        private const string RibbonPanelName = "Hot Reload";
        private const string ButtonText = "Manual Reload";

        private static RuntimeManager runtimeManager;
        private PipeServer pipeServer;

        /// <summary>
        /// Gets the current runtime manager instance.
        /// </summary>
        public static RuntimeManager RuntimeManager => runtimeManager;

        /// <summary>
        /// Called when Revit starts up.
        /// </summary>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Create hot reload ribbon tab and panel
                try { application.CreateRibbonTab(RibbonTabName); } catch { }
                var panel = application.CreateRibbonPanel(RibbonTabName, RibbonPanelName);

                // Create manual reload button
                var buttonData = new PushButtonData(
                    "ManualReload",
                    ButtonText,
                    Assembly.GetExecutingAssembly().Location,
                    typeof(ReloadRuntimeCommand).FullName);
                panel.AddItem(buttonData);

                // Initialize runtime manager
                runtimeManager = new RuntimeManager();

                // Start pipe server for build system communication
                pipeServer = new PipeServer(runtimeManager);
                pipeServer.Start();

                // Load initial runtime if available
                runtimeManager.LoadInitialRuntime(application);

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
                pipeServer?.Stop();
                runtimeManager?.Shutdown();
                runtimeManager = null;
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Loader Error", $"Error during shutdown: {ex.Message}");
                return Result.Failed;
            }
        }
    }
}