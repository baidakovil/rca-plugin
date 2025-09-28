using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Rca.Loader.Restart;

namespace Rca.Loader.Commands
{
    /// <summary>
    /// External command to reload the runtime or restart Revit if loader needs updating.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ReloadRuntimeCommand : IExternalCommand
    {
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
                        MainContent = "A new version of the Loader components is available. " +
                                     "Revit must be restarted to use the new version.\n\n" +
                                     "Would you like to restart Revit now or just reload the Runtime?",
                        CommonButtons = TaskDialogCommonButtons.None
                    };

                    td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Restart Revit", "Close Revit and restart with the new Loader version");
                    td.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Just Reload Runtime", "Keep using the current Loader but reload Runtime");
                    td.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "Cancel", "Don't reload or restart");

                    var result = td.Show();

                    switch (result)
                    {
                        case TaskDialogResult.CommandLink1:
                            // Restart Revit
                            var restartManager = new RestartManager(LoaderApp.Instance.AssemblyStatusManager);
                            if (restartManager.ShowRestartDialog())
                            {
                                // Restart initiated, return success
                                return Result.Succeeded;
                            }
                            return Result.Cancelled;

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
                    var success = LoaderApp.Instance.RuntimeManager.ReloadLatest(out var error);
                    if (success)
                    {
                        // Update runtime hash after successful reload
                        LoaderApp.Instance.AssemblyStatusManager?.UpdateHashesAfterReload(
                            LoaderApp.Instance.RuntimeManager.CurrentRuntimePath);

                        TaskDialog.Show("RCA Loader", "Runtime reloaded successfully!");
                        return Result.Succeeded;
                    }
                    else
                    {
                        message = error ?? "Unknown error";
                        TaskDialog.Show("RCA Loader Error", $"Failed to reload runtime: {error}");
                        return Result.Failed;
                    }
                }

                // Runtime is loaded - check whether it is outdated
                bool runtimeOutdated = LoaderApp.Instance.AssemblyStatusManager?.IsRuntimeOutdated() ?? false;
                if (!runtimeOutdated)
                {
                    TaskDialog.Show("RCA Loader", "All assemblies are up to date. No reload needed.");
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
                    return Result.Succeeded;
                }
                else
                {
                    message = reloadError ?? "Unknown error";
                    TaskDialog.Show("RCA Loader Error", $"Failed to reload runtime: {reloadError}");
                    return Result.Failed;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("RCA Loader Error", $"Error reloading runtime: {ex.Message}");
                return Result.Failed;
            }
        }
    }
}
