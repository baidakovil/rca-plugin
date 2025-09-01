using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Rca.Loader
{
    /// <summary>
    /// External command for manually triggering runtime reload.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ReloadRuntimeCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Find the LoaderApp instance to access RuntimeManager
                // For now, we'll create a new RuntimeManager instance
                // In production, this could be improved with a singleton pattern
                var runtimeManager = new RuntimeManager();
                runtimeManager.Reload(force: true);
                
                TaskDialog.Show("RCA Loader", "Runtime reloaded successfully!");
                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("RCA Loader Error", $"Failed to reload runtime: {ex.Message}");
                return Result.Failed;
            }
        }
    }
}