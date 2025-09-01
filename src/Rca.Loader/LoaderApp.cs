using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Rca.Loader.Contracts;
using System;
using System.Reflection;

namespace Rca.Loader
{
    /// <summary>
    /// The stable loader application that manages hot-reloadable runtime assemblies.
    /// </summary>
    public class LoaderApp : IExternalApplication
    {
        private const string LoaderTabName = "RCA Loader";
        private const string LoaderPanelName = "Hot Reload";
        private RuntimeManager runtimeManager;
        private PipeServer pipeServer;

        /// <summary>
        /// Called when Revit starts up.
        /// </summary>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Create ribbon tab and panel for loader controls
                try { application.CreateRibbonTab(LoaderTabName); } catch { }
                var panel = application.CreateRibbonPanel(LoaderTabName, LoaderPanelName);

                // Create manual reload button
                var reloadButtonData = new PushButtonData(
                    "ManualReload",
                    "Reload Runtime",
                    Assembly.GetExecutingAssembly().Location,
                    typeof(ReloadRuntimeCommand).FullName);
                panel.AddItem(reloadButtonData);

                // Initialize runtime manager
                runtimeManager = new RuntimeManager();
                
                // Start pipe server for build notifications
                pipeServer = new PipeServer(runtimeManager);
                pipeServer.Start();

                // Load initial runtime
                runtimeManager.LoadRuntime(application);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Loader Error", $"Failed to start loader: {ex.Message}");
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
                runtimeManager?.UnloadCurrentRuntime();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Loader Warning", $"Error during shutdown: {ex.Message}");
                return Result.Succeeded; // Don't fail shutdown
            }
        }
    }
}