using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace Rca.Loader
{
    /// <summary>
    /// External command for manually triggering runtime reload.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class ReloadRuntimeCommand : IExternalCommand
    {
        /// <summary>
        /// Executes the reload runtime command.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var runtimeManager = LoaderApp.RuntimeManager;
                if (runtimeManager == null)
                {
                    message = "Runtime manager not available";
                    return Result.Failed;
                }

                runtimeManager.Reload(force: true);
                TaskDialog.Show("Manual Reload", "Runtime reload triggered successfully.");
                
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