using Autodesk.Revit.UI;
using Rca.Contracts;
using System;

namespace Rca.UI.Views
{
    public class RcaDockablePanelProvider : IDockablePaneProvider
    {
        private readonly Func<UIApplication> uiappProvider;
        private readonly IPythonExecutionService pythonService;
        private readonly IDebugLogService debugLogService;

        public RcaDockablePanelProvider(
            Func<UIApplication> uiappProvider,
            IPythonExecutionService pythonService,
            IDebugLogService debugLogService)
        {
            this.uiappProvider = uiappProvider;
            this.pythonService = pythonService;
            this.debugLogService = debugLogService;
        }

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