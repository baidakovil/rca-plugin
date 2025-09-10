using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Rca.Loader.Commands
{
    /// <summary>
    /// Command to open the RCA standalone assistant window.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class OpenStandaloneWindowCommand : IExternalCommand
    {
        /// <summary>
        /// Executes the command to open the standalone assistant window.
        /// </summary>
        /// <param name="commandData">External command data.</param>
        /// <param name="message">Error message (if any).</param>
        /// <param name="elements">Elements to highlight (if any).</param>
        /// <returns>Result of the command execution.</returns>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (LoaderApp.Instance == null)
            {
                message = "Loader instance unavailable";
                return Result.Failed;
            }
            
            var runtimeManager = LoaderApp.Instance.RuntimeManager;
            if (!runtimeManager.ShowStandaloneWindow(out var error))
            {
                TaskDialog.Show("RCA Loader", error ?? "Unknown error opening window");
                message = error ?? string.Empty;
                return Result.Failed;
            }
            
            return Result.Succeeded;
        }
    }
}