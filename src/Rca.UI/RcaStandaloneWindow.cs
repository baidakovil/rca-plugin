using Rca.Contracts;
using Rca.Contracts.Infrastructure;
using System;
using System.Windows;

namespace Rca.UI.Views
{
    /// <summary>
    /// Window for displaying RcaDockablePanel outside of dockable panel in Revit.
    /// </summary>
    public class RcaStandaloneWindow : Window
    {
        public RcaStandaloneWindow()
        {
            Title = "RCA Chat Assistant (Standalone)";
            Width = 400;
            Height = 600;
            
            try
            {
                // Use dependency injection to create the content
                var container = ServiceContainer.Instance;
                
                // Get required services or provide fallbacks
                var revitContext = GetServiceOrDefault<IRevitContext>(container);
                var pythonService = GetServiceOrDefault<IPythonExecutionService>(container);
                var debugLogService = GetServiceOrDefault<IDebugLogService>(container);
                
                Content = new RcaDockablePanel(
                    () => revitContext?.CurrentUIApplication as Autodesk.Revit.UI.UIApplication,
                    pythonService,
                    () => new DebugInfoWindow(debugLogService));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error initializing standalone window: {ex.Message}\n\nThis is likely due to missing service registrations.", 
                    "RCA Standalone Window Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
        }
        
        private T GetServiceOrDefault<T>(ServiceContainer container) where T : class
        {
            try
            {
                return container.Resolve<T>();
            }
            catch (InvalidOperationException)
            {
                MessageBox.Show(
                    $"Service {typeof(T).Name} not registered. Some functionality may be limited.", 
                    "RCA Service Warning", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Warning);
                
                // Return null for the service - calling code must handle this
                return null;
            }
        }
    }
}
