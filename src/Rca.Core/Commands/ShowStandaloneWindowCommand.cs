using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Rca.UI.ChatPanel;
using System;

namespace Rca.Core.Commands
{
    /// <summary>
    /// Command to show a standalone window for the RCA Chat Assistant.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ShowStandaloneWindowCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, Autodesk.Revit.DB.ElementSet elements)
        {
            try
            {
                // Store UIApplication for use by standalone window
                Rca.Core.RevitContext.CurrentUIApplication = commandData.Application;
                var window = new RcaStandaloneWindow();
                window.Show();
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
