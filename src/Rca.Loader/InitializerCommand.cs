using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Diagnostics;

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
            try
            {
                Debug.WriteLine("DEBUG: InitializerCommand.Execute called");
                
                if (LoaderApp.Instance == null)
                {
                    message = "LoaderApp instance not initialized";
                    Debug.WriteLine("DEBUG: LoaderApp instance is null");
                    TaskDialog.Show("RCA Loader Error", "LoaderApp instance is null. Cannot initialize.");
                    return Result.Failed;
                }
                
                Debug.WriteLine($"DEBUG: LoaderApp instance exists, initializing with UIApplication");
                
                // Initialize the loader with the UIApplication
                LoaderApp.Instance.InitializeWithUIApplication(commandData.Application);
                
                Debug.WriteLine("DEBUG: UIApplication initialized successfully");
                
                // Show a success message
                TaskDialog.Show("RCA Loader", "Initialization successful! The pipe server is now running.");
                
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DEBUG: InitializerCommand.Execute exception: {ex}");
                message = ex.Message;
                TaskDialog.Show("RCA Loader Error", $"Failed to initialize: {ex.Message}\n\nStack trace: {ex.StackTrace}");
                return Result.Failed;
            }
        }
    }
}