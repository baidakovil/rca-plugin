using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Rca.Loader.Commands
{
    /// <summary>
    /// Command to reload the latest runtime.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ReloadRuntimeCommand : IExternalCommand
    {
        /// <summary>
        /// Executes the command to reload the latest runtime version.
        /// </summary>
        /// <param name="commandData">External command data.</param>
        /// <param name="message">Error message (if any).</param>
        /// <param name="elements">Elements to highlight (if any).</param>
        /// <returns>Result of the command execution.</returns>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (LoaderApp.Instance == null)
            {
                message = "Loader instance unavailable";
                return Result.Failed;
            }
            
            var runtimeManager = LoaderApp.Instance.RuntimeManager;
            if (!runtimeManager.ReloadLatest(out var error))
            {
                TaskDialog.Show("RCA Loader", error ?? "Reload failed");
                message = error ?? string.Empty;
                return Result.Failed;
            }
            
            TaskDialog.Show("RCA Loader", "Runtime reloaded successfully");
            return Result.Succeeded;
        }
    }
}