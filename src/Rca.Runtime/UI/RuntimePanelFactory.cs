extern alias LoaderMerged;
using System;
using System.Windows;
using LoaderMerged::Rca.Loader.Contracts;
using Rca.Contracts;
using Rca.UI.Views;
using Microsoft.Extensions.Logging;
using Rca.Runtime.Logging;
using Autodesk.Revit.UI;

namespace Rca.Runtime.UI
{
    /// <summary>
    /// Factory implementation that creates the dockable panel UI for the runtime.
    /// Resolves required dependencies from SharedServiceRegistry and constructs RcaDockablePanel.
    /// </summary>
    public class RuntimePanelFactory : IRuntimePanelFactory
    {
        private readonly ILogger _log;

        /// <summary>
        /// Initializes a new instance of the RuntimePanelFactory class.
        /// Uses the same logger provider as RuntimeEntry for consistent logging.
        /// </summary>
        public RuntimePanelFactory()
        {
            // Get the shared logger provider from RuntimeEntry
            var provider = RuntimeEntry.GetLoggerProvider();
            _log = provider.CreateLogger(nameof(RuntimePanelFactory));
        }

        /// <summary>
        /// Creates the dockable panel FrameworkElement.
        /// </summary>
        /// <returns>FrameworkElement for the panel, or null on failure.</returns>
        public FrameworkElement? CreatePanel()
        {
            try
            {
                _log.LogInformation("CreatePanel called - resolving dependencies");
                
                // Resolve dependencies from SharedServiceRegistry
                var pythonService = SharedServiceRegistry.Resolve<IPythonExecutionService>();
                if (pythonService == null)
                {
                    _log.LogWarning("IPythonExecutionService not registered - cannot create panel");
                    return null;
                }
                
                _log.LogDebug("IPythonExecutionService resolved successfully (type={Type})", pythonService.GetType().FullName);

                // Create UIApplication provider - for now returns null, can be enhanced later
                Func<UIApplication?> uiappProvider = () => null;

                _log.LogDebug("Creating RcaDockablePanel instance");
                
                // Construct the panel with resolved dependencies
                var panel = new RcaDockablePanel(uiappProvider, pythonService);
                
                _log.LogInformation("Panel created successfully (type={Type})", panel?.GetType().FullName);
                return panel;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error creating panel: {Message}", ex.Message);
                return null;
            }
        }
    }
}
