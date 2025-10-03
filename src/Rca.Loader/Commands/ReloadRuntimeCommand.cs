using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Rca.Loader.Restart;
using Rca.Loader; // ensure LoaderApp access
using Rca.Loader.Logging;
using Microsoft.Extensions.Logging;

namespace Rca.Loader.Commands
{
    /// <summary>
    /// External command to reload the runtime or restart Revit if loader needs updating.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ReloadRuntimeCommand : IExternalCommand
    {
        private static readonly ILogger Log = LoaderLog.GetLogger<ReloadRuntimeCommand>();

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="commandData">The command data.</param>
        /// <param name="message">Error message.</param>
        /// <param name="elements">Elements for errors.</param>
        /// <returns>Result of the command.</returns>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (LoaderApp.Instance?.RuntimeManager == null)
                {
                    message = "Runtime manager not available";
                    TaskDialog.Show("RCA Loader Error", "Runtime manager is not available. Please restart Revit.");
                    Log.LogWarning("Execute aborted: RuntimeManager missing");
                    return Result.Failed;
                }

                // Check if loader is outdated first
                if (LoaderApp.Instance.AssemblyStatusManager?.IsLoaderOutdated() == true)
                {
                    // Show dialog with options to restart Revit or just reload runtime
                    var td = new TaskDialog("Loader Update Available")
                    {
                        MainIcon = TaskDialogIcon.TaskDialogIconWarning,
                        MainInstruction = "Loader components are outdated",
                        MainContent = "A new version of the Loader components is available. Revit must be restarted to use the new version.\n\n" +
                                     "Would you like to restart Revit now or just reload the Runtime?",
                        CommonButtons = TaskDialogCommonButtons.None
                    };

                    td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Restart Revit", "Close Revit and restart with the new Loader version");
                    td.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Just Reload Runtime", "Keep using the current Loader but reload Runtime");
                    td.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "Cancel", "Don't reload or restart");

                    var result = td.Show();
                    Log.LogInformation("Loader outdated dialog result={Result}", result);
                    switch (result)
                    {
                        case TaskDialogResult.CommandLink1:
                            // Restart Revit
                            var restartManager = new RestartManager(LoaderApp.Instance.AssemblyStatusManager);
                            bool restart = restartManager.ShowRestartDialog();
                            Log.LogInformation("Restart dialog invoked restart={Restart}", restart);
                            return restart ? Result.Succeeded : Result.Cancelled;

                        case TaskDialogResult.CommandLink2:
                            // Just reload runtime, continue with normal flow
                            break;

                        default:
                            // Cancel
                            return Result.Cancelled;
                    }
                }

                // If runtime is not currently loaded, attempt to load the latest runtime directly
                var runtimeLoaded = LoaderApp.Instance.RuntimeManager.IsRuntimeLoaded;
                if (!runtimeLoaded)
                {
                    Log.LogInformation("Runtime not loaded - performing initial ReloadLatest");
                    var success = LoaderApp.Instance.RuntimeManager.ReloadLatest(out var error);
                    if (success)
                    {
                        // Update runtime hash after successful reload
                        LoaderApp.Instance.AssemblyStatusManager?.UpdateHashesAfterReload(
                            LoaderApp.Instance.RuntimeManager.CurrentRuntimePath);

                        TaskDialog.Show("RCA Loader", "Runtime reloaded successfully!");

                        // Attempt to create runtime UI and set it into the dockable panel host
                        TryReplaceDockableContent();

                        return Result.Succeeded;
                    }
                    message = error ?? "Unknown error";
                    Log.LogWarning("Initial runtime load failed error={Error}", error);
                    TaskDialog.Show("RCA Loader Error", $"Failed to reload runtime: {error}");
                    return Result.Failed;
                }

                // Runtime is loaded - check whether it is outdated
                bool runtimeOutdated = LoaderApp.Instance.AssemblyStatusManager?.IsRuntimeOutdated() ?? false;
                if (!runtimeOutdated)
                {
                    TaskDialog.Show("RCA Loader", "All assemblies are up to date. No reload needed.");
                    Log.LogInformation("Reload not needed - assemblies up to date");
                    return Result.Succeeded;
                }

                // Normal runtime reload flow (runtime was loaded and flagged outdated)
                var reloadSuccess = LoaderApp.Instance.RuntimeManager.ReloadLatest(out var reloadError);

                if (reloadSuccess)
                {
                    // Update runtime hash after successful reload
                    LoaderApp.Instance.AssemblyStatusManager?.UpdateHashesAfterReload(
                        LoaderApp.Instance.RuntimeManager.CurrentRuntimePath);

                    TaskDialog.Show("RCA Loader", "Runtime reloaded successfully!");
                    Log.LogInformation("Runtime reloaded successfully (outdated path updated)");

                    // Replace dockable content with runtime UI
                    TryReplaceDockableContent();

                    return Result.Succeeded;
                }
                message = reloadError ?? "Unknown error";
                Log.LogWarning("Runtime reload failed error={Error}", reloadError);
                TaskDialog.Show("RCA Loader Error", $"Failed to reload runtime: {reloadError}");
                return Result.Failed;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                Log.LogError(ex, "Unhandled exception in ReloadRuntimeCommand.Execute");
                TaskDialog.Show("RCA Loader Error", $"Error reloading runtime: {ex.Message}");
                return Result.Failed;
            }
        }

        private void TryReplaceDockableContent()
        {
            try
            {
                var host = LoaderApp.Instance?.PanelHost;
                if (host == null)
                {
                    Log.LogWarning("PanelHost unavailable for content replacement");
                    return;
                }
                var runtimeManager = LoaderApp.Instance?.RuntimeManager;
                if (runtimeManager == null)
                {
                    Log.LogWarning("RuntimeManager null during content replacement");
                    return;
                }
                var content = runtimeManager.CreateRuntimeDockableContent(out var createError);
                if (content != null)
                {
                    host.SetContent(content);
                    Log.LogInformation("Dockable content replaced successfully contentType={Type}", content.GetType().FullName);
                }
                else
                {
                    Log.LogWarning("Failed to create runtime dockable content error={Error}", createError);
                }
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "Error replacing dockable content");
            }
        }
    }
}
