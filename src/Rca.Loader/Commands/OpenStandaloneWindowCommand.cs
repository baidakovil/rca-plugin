using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

namespace Rca.Loader.Commands
{
    /// <summary>
    /// External command to open the standalone window.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class OpenStandaloneWindowCommand : IExternalCommand
    {
        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="commandData">The command data.</param>
        /// <param name="message">Error message.</param>
        /// <param name="elements">Elements for errors.</param>
        /// <returns>Result of the command.</returns>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (LoaderApp.Instance?.RuntimeManager == null)
                {
                    message = "Runtime manager not available";
                    TaskDialog.Show("RCA Loader Error", "Runtime manager is not available. Please restart Revit.");
                    return Result.Failed;
                }

                var success = LoaderApp.Instance.RuntimeManager.ShowStandaloneWindow(out var error);
                
                if (success)
                {
                    return Result.Succeeded;
                }
                else
                {
                    message = error ?? "Unknown error";
                    TaskDialog.Show("RCA Loader Error", $"Failed to open standalone window: {error}");
                    return Result.Failed;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("RCA Loader Error", $"Error opening standalone window: {ex.Message}");
                return Result.Failed;
            }
        }
    }
}