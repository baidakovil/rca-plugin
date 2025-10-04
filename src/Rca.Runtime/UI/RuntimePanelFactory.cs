using System;
using System.Windows;
using Autodesk.Revit.UI;
using Rca.Contracts;
using Rca.Loader.Contracts;
using Rca.UI.Views;
using Microsoft.Extensions.Logging;
using Rca.Runtime.Logging;

namespace Rca.Runtime.UI
{
    /// <summary>
    /// Factory implementation that creates the dockable panel UI for the runtime.
    /// Resolves required dependencies from SharedServiceRegistry and constructs RcaDockablePanel.
    /// </summary>
    public class RuntimePanelFactory : IRuntimePanelFactory
    {
        private readonly ILogger _log;
        private static NamedPipeLoggerProvider? _provider;
        private static readonly string SessionId = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Initializes a new instance of the RuntimePanelFactory class.
        /// </summary>
        public RuntimePanelFactory()
        {
            _provider ??= new NamedPipeLoggerProvider("RCA_LOG_PIPE", SessionId);
            _log = _provider.CreateLogger(nameof(RuntimePanelFactory));
        }

        /// <summary>
        /// Creates the dockable panel FrameworkElement.
        /// </summary>
        /// <returns>FrameworkElement for the panel, or null on failure.</returns>
        public FrameworkElement? CreatePanel()
        {
            try
            {
                // Resolve dependencies from SharedServiceRegistry
                var pythonService = SharedServiceRegistry.Resolve<IPythonExecutionService>();
                if (pythonService == null)
                {
                    _log.LogWarning("IPythonExecutionService not registered - cannot create panel");
                    return null;
                }

                // Create UIApplication provider - for now returns null, can be enhanced later
                Func<UIApplication?> uiappProvider = () => null;

                // Construct the panel with resolved dependencies
                var panel = new RcaDockablePanel(uiappProvider, pythonService);
                
                _log.LogInformation("Panel created successfully");
                return panel;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error creating panel");
                return null;
            }
        }
    }
}
