using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace Rca.Loader.Commands
{
    /// <summary>
    /// Command to manually reload the runtime.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ReloadRuntimeCommand : IExternalCommand
    {
        /// <summary>
        /// Executes the reload runtime command.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Find the runtime manager from the static instance
                var runtimeManager = LoaderApp.GetRuntimeManager();
                if (runtimeManager == null)
                {
                    TaskDialog.Show("Manual Reload", "Runtime manager not available. Ensure the Loader is properly initialized.");
                    return Result.Failed;
                }

                // Trigger manual reload
                runtimeManager.Reload();
                
                TaskDialog.Show("Manual Reload", "Runtime reload triggered successfully!");
                
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Manual Reload Error", $"Failed to reload runtime: {ex.Message}");
                return Result.Failed;
            }
        }
    }
}