using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using RcaPlugin.Views;
using System;

namespace RcaPlugin.Commands
{
    /// <summary>
    /// ??????? ??? ???????? RcaDockablePanel ? ????????? ???? (??? ??????? ??? ??????????? ??????).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ShowStandaloneWindowCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, Autodesk.Revit.DB.ElementSet elements)
        {
            try
            {
                // Store UIApplication for use by standalone window
                RcaPlugin.RevitContext.CurrentUIApplication = commandData.Application;
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
