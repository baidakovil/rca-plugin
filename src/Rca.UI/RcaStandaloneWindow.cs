using Autodesk.Revit.UI;
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
                var pythonService = GetServiceOrDefault<IPythonExecutionService>(container) 
                    ?? new NullPythonExecutionService();
                var debugLogService = GetServiceOrDefault<IDebugLogService>(container) 
                    ?? new NullDebugLogService();
                
                // Create a UIApplication provider that safely handles null context
                Func<UIApplication?> uiAppProvider = () => {
                    if (revitContext?.CurrentUIApplication is UIApplication uiApp)
                        return uiApp;
                    return null;
                };
                
                Content = new RcaDockablePanel(
                    uiAppProvider,
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
        
        private T? GetServiceOrDefault<T>(ServiceContainer container) where T : class
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
        
        /// <summary>
        /// Null implementation for standalone mode
        /// </summary>
        private class NullPythonExecutionService : IPythonExecutionService
        {
            public System.Threading.Tasks.Task<string> ExecuteAsync(string code)
            {
                return System.Threading.Tasks.Task.FromResult(
                    "Python execution not available in standalone mode.");
            }

            public string ExecuteSync(string code)
            {
                return "Python execution not available in standalone mode.";
            }

            public void SetRevitContext(object context)
            {
                // Do nothing
            }
        }
        
        /// <summary>
        /// Null implementation for standalone mode
        /// </summary>
        private class NullDebugLogService : IDebugLogService
        {
            public System.Collections.ObjectModel.ReadOnlyObservableCollection<IDebugLogEntry> Entries => 
                new System.Collections.ObjectModel.ReadOnlyObservableCollection<IDebugLogEntry>(
                    new System.Collections.ObjectModel.ObservableCollection<IDebugLogEntry>());
                    
            public void LogError(string message)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] {message}");
            }

            public void LogInfo(string message)
            {
                System.Diagnostics.Debug.WriteLine($"[INFO] {message}");
            }

            public void LogPythonOutput(string message)
            {
                System.Diagnostics.Debug.WriteLine($"[PYTHON] {message}");
            }
            
            public void LogCustom(string message, DebugLogType type)
            {
                System.Diagnostics.Debug.WriteLine($"[{type}] {message}");
            }
        }
    }
}
