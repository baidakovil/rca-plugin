using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Rca.Loader.Commands
{
    /// <summary>
    /// External command to reload the runtime.
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

                var success = LoaderApp.Instance.RuntimeManager.ReloadLatest(out var error);
                
                if (success)
                {
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
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("RCA Loader Error", $"Error reloading runtime: {ex.Message}");
                return Result.Failed;
            }
        }
    }
}