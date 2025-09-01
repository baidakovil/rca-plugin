using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Rca.Loader.Services;

namespace Rca.Loader.Commands
{
    /// <summary>
    /// Manual reload command for testing and fallback scenarios.
    /// Triggers a runtime reload without requiring the build system.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ReloadRuntimeCommand : IExternalCommand
    {
        /// <summary>
        /// Executes the manual reload command.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var runtimeManager = RuntimeManager.Current;
                if (runtimeManager == null)
                {
                    message = "Runtime manager not found. Loader may not be properly initialized.";
                    return Result.Failed;
                }

                // Show current status and reload
                var currentVersion = runtimeManager.CurrentRuntimeVersion;
                var isLoaded = runtimeManager.IsRuntimeLoaded;
                
                var success = runtimeManager.Reload(force: true);
                
                var statusMessage = success 
                    ? $"Runtime reloaded successfully!\n\nPrevious: {(isLoaded ? currentVersion : "None")}\nCurrent: {runtimeManager.CurrentRuntimeVersion}"
                    : "Runtime reload failed. Check console for details.";
                
                TaskDialog.Show("Manual Reload", statusMessage);
                
                return success ? Result.Succeeded : Result.Failed;
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}