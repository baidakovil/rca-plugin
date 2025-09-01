using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Rca.Loader.Commands;
using System;
using System.Reflection;

namespace Rca.Loader
{
    /// <summary>
    /// The main external application class for the RCA Loader.
    /// This is the stable loader that manages the hot-reloadable runtime.
    /// </summary>
    public class LoaderApp : IExternalApplication
    {
        private const string LoaderTabName = "RCA Loader";
        private const string LoaderPanelName = "Hot Reload";
        private const string ReloadButtonText = "Reload Runtime";

        private RuntimeManager runtimeManager;
        private PipeServer pipeServer;

        /// <summary>
        /// Called when Revit starts up.
        /// </summary>
        /// <param name="application">The UI application.</param>
        /// <returns>The startup result.</returns>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Initialize runtime manager
                runtimeManager = new RuntimeManager();
                runtimeManager.Initialize(application);

                // Set up static reference for manual reload command
                ReloadRuntimeCommand.RuntimeManager = runtimeManager;

                // Create loader ribbon tab and panel
                CreateLoaderUI(application);

                // Start pipe server for build system communication
                pipeServer = new PipeServer(runtimeManager);
                pipeServer.Start();

                // Set up global exception handlers
                SetupExceptionHandlers();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Loader Error", $"Failed to start RCA Loader: {ex.Message}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Called when Revit shuts down.
        /// </summary>
        /// <param name="application">The UI application.</param>
        /// <returns>The shutdown result.</returns>
        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                // Stop pipe server
                pipeServer?.Stop();
                pipeServer?.Dispose();

                // Shutdown runtime manager
                runtimeManager?.Shutdown();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during loader shutdown: {ex.Message}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Creates the loader UI components.
        /// </summary>
        /// <param name="application">The UI application.</param>
        private void CreateLoaderUI(UIControlledApplication application)
        {
            // Create ribbon tab and panel for loader controls
            try { application.CreateRibbonTab(LoaderTabName); } catch { }
            var panel = application.CreateRibbonPanel(LoaderTabName, LoaderPanelName);

            // Create manual reload button
            var reloadButtonData = new PushButtonData(
                "ManualReloadRuntime",
                ReloadButtonText,
                Assembly.GetExecutingAssembly().Location,
                typeof(ReloadRuntimeCommand).FullName);
            
            var reloadButton = panel.AddItem(reloadButtonData) as PushButton;
            reloadButton.ToolTip = "Manually reload the RCA runtime from the latest build.";
        }

        /// <summary>
        /// Sets up global exception handlers to catch runtime errors.
        /// </summary>
        private void SetupExceptionHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                try
                {
                    var ex = e.ExceptionObject as Exception;
                    var message = ex?.Message ?? "Unknown error";
                    System.Diagnostics.Debug.WriteLine($"Unhandled exception in RCA: {message}");
                    
                    // Could send RUNTIME_ERROR event here if needed
                }
                catch
                {
                    // Avoid exceptions in exception handlers
                }
            };

            // WPF dispatcher exception handler (if WPF is used)
            try
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.UnhandledException += (sender, e) =>
                {
                    try
                    {
                        var message = e.Exception?.Message ?? "Unknown WPF error";
                        System.Diagnostics.Debug.WriteLine($"Unhandled WPF exception in RCA: {message}");
                        e.Handled = true; // Prevent crash
                    }
                    catch
                    {
                        // Avoid exceptions in exception handlers
                    }
                };
            }
            catch
            {
                // WPF might not be available in some contexts
            }
        }
    }
}