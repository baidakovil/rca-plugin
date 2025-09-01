using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System;

namespace Rca.Loader
{
    /// <summary>
    /// The stable loader application that manages hot-reloadable runtime.
    /// This class is never unloaded and provides the stable entry point for Revit.
    /// </summary>
    public class LoaderApp : IExternalApplication
    {
        private static LoaderApp _instance;
        private RuntimeManager runtimeManager;

        /// <summary>
        /// Called when Revit starts up.
        /// </summary>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                _instance = this;
                
                // Initialize the runtime manager
                runtimeManager = new RuntimeManager();
                
                // Setup loader ribbon for manual control
                SetupLoaderRibbon(application);
                
                // Start the named pipe server for build notifications
                runtimeManager.StartPipeServer();
                
                // Load the initial runtime
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
                runtimeManager?.Shutdown();
                _instance = null;
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Loader Error", $"Error during shutdown: {ex.Message}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Sets up the loader control ribbon.
        /// </summary>
        private void SetupLoaderRibbon(UIControlledApplication application)
        {
            try
            {
                // Create loader tab and panel
                const string tabName = "RCA Loader";
                const string panelName = "Hot Reload";
                
                try { application.CreateRibbonTab(tabName); } catch { }
                var panel = application.CreateRibbonPanel(tabName, panelName);

                // Create manual reload button
                var reloadButtonData = new PushButtonData(
                    "ManualReload",
                    "Reload Runtime",
                    System.Reflection.Assembly.GetExecutingAssembly().Location,
                    typeof(Commands.ReloadRuntimeCommand).FullName);
                
                reloadButtonData.ToolTip = "Manually reload the RCA runtime from the latest build";
                panel.AddItem(reloadButtonData);
            }
            catch (Exception ex)
            {
                // Log but don't fail startup for ribbon issues
                TaskDialog.Show("RCA Loader Warning", $"Could not create loader ribbon: {ex.Message}");
            }
        }
    }
}
}