using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Rca.Contracts;
using Rca.Contracts.Infrastructure;
using RcaPlugin.Infrastructure;
using System;

namespace RcaPlugin.Commands
{
    /// <summary>
    /// Helper to allow unit testing of context update without requiring Revit API types in tests.
    /// </summary>
    internal static class RevitContextHelper
    {
        public static void UpdateContext(IRevitContext context, object uiApplication)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            // We deliberately accept object to avoid forcing Revit API reference in unit tests.
            context.CurrentUIApplication = uiApplication;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class ShowDockablePanelCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Update the Revit context with current UIApplication via helper for testability
                var container = ServiceContainer.Instance;
                var revitContext = container.Resolve<IRevitContext>();
                RevitContextHelper.UpdateContext(revitContext, commandData.Application);

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