using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace Rca.Loader.Commands
{
    /// <summary>
    /// Manual command to trigger runtime reload for testing purposes.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ReloadRuntimeCommand : IExternalCommand
    {
        /// <summary>
        /// Executes the manual reload command.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Find the runtime manager (this is a simple approach; in production might use DI)
                var loaderApp = FindLoaderApp();
                if (loaderApp != null)
                {
                    // Use reflection to access the runtime manager and trigger reload
                    var runtimeManagerField = typeof(LoaderApp).GetField("runtimeManager", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (runtimeManagerField?.GetValue(loaderApp) is RuntimeManager runtimeManager)
                    {
                        runtimeManager.Reload(force: true);
                        TaskDialog.Show("RCA Loader", "Manual reload triggered successfully.");
                        return Result.Succeeded;
                    }
                }

                TaskDialog.Show("RCA Loader", "Could not find runtime manager to trigger reload.");
                return Result.Failed;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("RCA Loader Error", $"Manual reload failed: {ex.Message}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Finds the LoaderApp instance (simplified approach).
        /// </summary>
        private LoaderApp FindLoaderApp()
        {
            // This is a simplified approach for finding the loader app instance
            // In a production system, this could be managed through a singleton or DI container
            try
            {
                // Look for the LoaderApp in the current AppDomain assemblies
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == "Rca.Loader")
                    {
                        var loaderAppType = assembly.GetType("Rca.Loader.LoaderApp");
                        if (loaderAppType != null)
                        {
                            // For now, we'll use a static field approach in production
                            // This would be better handled with proper DI
                            var instanceField = loaderAppType.GetField("_instance", 
                                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                            return instanceField?.GetValue(null) as LoaderApp;
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors in reflection
            }

            return null;
        }
    }
}