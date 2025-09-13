using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

namespace Rca.Loader
{
    /// <summary>
    /// External command to initialize the RCA loader with UIApplication.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class InitializerCommand : IExternalCommand
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
            if (commandData == null)
                throw new ArgumentNullException(nameof(commandData));
            try
            {
                if (LoaderApp.Instance == null)
                {
                    message = "LoaderApp instance not initialized";
                    TaskDialog.Show("RCA Loader Error", "LoaderApp instance is null. Cannot initialize.");
                    return Result.Failed;
                }
                
                // Initialize the loader with the UIApplication
                LoaderApp.Instance.InitializeWithUIApplication(commandData.Application);
                
                // Show a success message
                TaskDialog.Show("RCA Loader", "Initialization successful! The pipe server is now running.");
                
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("RCA Loader Error", $"Failed to initialize: {ex.Message}\n\nStack trace: {ex.StackTrace}");
                return Result.Failed;
            }
        }
    }
}
