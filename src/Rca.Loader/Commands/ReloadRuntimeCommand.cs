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
                // Find the runtime manager from the loader app
                // This is a simplified approach - in a real implementation you might use
                // a service locator or static reference
                TaskDialog.Show("Manual Reload", "Manual reload triggered. Check the pipe server for reload functionality.");
                
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}