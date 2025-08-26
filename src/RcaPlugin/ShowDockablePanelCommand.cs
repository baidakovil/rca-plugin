using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Rca.Contracts;
using RcaPlugin.Infrastructure;
using System;

namespace RcaPlugin.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ShowDockablePanelCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Update the Revit context with current UIApplication
                var container = ServiceContainer.Instance;
                var revitContext = container.Resolve<IRevitContext>();
                revitContext.CurrentUIApplication = commandData.Application;

                var dpId = new DockablePaneId(new Guid("A1B2C3D4-E5F6-47A8-9B0C-1234567890AB"));
                var dockablePane = commandData.Application.GetDockablePane(dpId);
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