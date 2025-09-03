using Autodesk.Revit.UI;
using Rca.Contracts;
using Rca.UI.ViewModels;
using System;
using System.Windows.Controls;

namespace Rca.UI.Views
{
    /// <summary>
    /// Interaction logic for RcaDockablePanel.xaml
    /// </summary>
    public partial class RcaDockablePanel : UserControl
    {
        public RcaDockablePanel(
            Func<UIApplication> uiappProvider, 
            IPythonExecutionService pythonService,
            Func<DebugInfoWindow> debugInfoWindowFactory)
        {
            InitializeComponent();
            
            // Handle potential null services in standalone mode
            if (pythonService == null)
            {
                System.Diagnostics.Debug.WriteLine("Warning: RcaDockablePanel created with null pythonService");
                pythonService = new NullPythonExecutionService();
            }
            
            if (debugInfoWindowFactory == null)
            {
                System.Diagnostics.Debug.WriteLine("Warning: RcaDockablePanel created with null debugInfoWindowFactory");
                debugInfoWindowFactory = () => new DebugInfoWindow(new NullDebugLogService());
            }
            
            DataContext = new RcaDockablePanelViewModel(uiappProvider, pythonService, debugInfoWindowFactory);
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