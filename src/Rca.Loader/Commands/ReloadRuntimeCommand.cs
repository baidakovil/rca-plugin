using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace Rca.Loader.Commands
{
    /// <summary>
    /// Command to manually trigger a runtime reload.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ReloadRuntimeCommand : IExternalCommand
    {
        /// <summary>
        /// The runtime manager instance.
        /// </summary>
        public static RuntimeManager RuntimeManager { get; set; }

        /// <summary>
        /// Executes the reload command.
        /// </summary>
        /// <param name="commandData">The command data.</param>
        /// <param name="message">The error message.</param>
        /// <param name="elements">The element set.</param>
        /// <returns>The execution result.</returns>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (RuntimeManager == null)
                {
                    TaskDialog.Show("RCA Loader", "Runtime manager not available.");
                    return Result.Failed;
                }

                RuntimeManager.Reload(force: true);
                TaskDialog.Show("RCA Loader", "Runtime reloaded successfully!");
                
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("RCA Loader Error", $"Failed to reload runtime: {ex.Message}");
                return Result.Failed;
            }
        }
    }
}