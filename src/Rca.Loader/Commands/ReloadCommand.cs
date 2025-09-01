using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Rca.Loader.Commands
{
    /// <summary>
    /// Manual reload command for triggering hot-reload from Revit UI (optional).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ReloadCommand : IExternalCommand
    {
        /// <summary>
        /// Executes the reload command.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // This is a placeholder - in practice, this would communicate with the loader
                // For now, just show a message indicating manual reload was triggered
                TaskDialog.Show("RCA Hot Reload", "Manual reload triggered. This feature requires the hot-reload server to be running.");
                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}