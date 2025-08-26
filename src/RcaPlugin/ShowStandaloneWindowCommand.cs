using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
#if WINDOWS
using Rca.UI.Views;
#endif
using Rca.Contracts;
using Rca.Contracts.Infrastructure;
using RcaPlugin.Infrastructure;
using System;

namespace RcaPlugin.Commands
{
    /// <summary>
    /// Command to open RcaDockablePanel in a standalone window (for testing or non-dockable mode).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ShowStandaloneWindowCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, Autodesk.Revit.DB.ElementSet elements)
        {
            try
            {
                // Update the Revit context with current UIApplication
                var container = ServiceContainer.Instance;
                var revitContext = container.Resolve<IRevitContext>();
                revitContext.CurrentUIApplication = commandData.Application;
                
#if WINDOWS
                var window = new RcaStandaloneWindow();
                window.Show();
#endif
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
