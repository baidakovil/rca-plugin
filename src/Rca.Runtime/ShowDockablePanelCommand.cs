using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace Rca.Runtime
{
    /// <summary>
    /// External command to show the dockable panel.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class ShowDockablePanelCommand : IExternalCommand
    {
        private const string DockablePaneGuid = "A1B2C3D4-E5F6-47A8-9B0C-1234567890AB";

        /// <summary>
        /// Executes the command to show the dockable panel.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var dpId = new DockablePaneId(new Guid(DockablePaneGuid));
                var dockablePane = commandData.Application.GetDockablePane(dpId);

                if (dockablePane != null)
                {
                    dockablePane.Show();
                    return Result.Succeeded;
                }
                else
                {
                    message = "Dockable pane not found";
                    return Result.Failed;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}