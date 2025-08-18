using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using RcaPlugin;
using RcaPlugin.Views;

namespace RcaPlugin.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ShowDockablePanelCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Store UIApplication for later use in dockable panel
                RcaPluginApp.SetUIApplication(commandData.Application);

                var dpId = new DockablePaneId(new Guid("A1B2C3D4-E5F6-47A8-9B0C-1234567890AB"));
                var dockablePane = commandData.Application.GetDockablePane(dpId);

                // Re-register provider with UIApplication if needed
                dockablePane?.Show();

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