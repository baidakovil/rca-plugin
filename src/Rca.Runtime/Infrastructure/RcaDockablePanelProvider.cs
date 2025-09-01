using Autodesk.Revit.UI;
using Rca.Contracts;
using Rca.UI.Views;
using System;

namespace Rca.Runtime.Infrastructure
{
    /// <summary>
    /// Dockable panel provider for the runtime.
    /// </summary>
    public class RcaDockablePanelProvider : IDockablePaneProvider
    {
        private readonly Func<UIApplication> uiappProvider;
        private readonly IPythonExecutionService pythonService;
        private readonly IDebugLogService debugLogService;

        /// <summary>
        /// Initializes a new instance of the RcaDockablePanelProvider class.
        /// </summary>
        public RcaDockablePanelProvider(
            Func<UIApplication> uiappProvider,
            IPythonExecutionService pythonService,
            IDebugLogService debugLogService)
        {
            this.uiappProvider = uiappProvider;
            this.pythonService = pythonService;
            this.debugLogService = debugLogService;
        }

        /// <summary>
        /// Sets up the dockable pane.
        /// </summary>
        public void SetupDockablePane(DockablePaneProviderData data)
        {
            // Create debug info window factory
            Func<DebugInfoWindow> debugInfoWindowFactory = () => new DebugInfoWindow(debugLogService);

            data.FrameworkElement = new RcaDockablePanel(uiappProvider, pythonService, debugInfoWindowFactory);
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Tabbed,
                TabBehind = DockablePanes.BuiltInDockablePanes.ProjectBrowser
            };
        }
    }
}