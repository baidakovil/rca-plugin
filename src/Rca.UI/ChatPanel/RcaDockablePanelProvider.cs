using Autodesk.Revit.UI;

namespace Rca.UI.ChatPanel
{
    public class RcaDockablePanelProvider : IDockablePaneProvider
    {
        public RcaDockablePanelProvider() { }

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            // Always resolve UIApplication at runtime from RevitContext
            data.FrameworkElement = new RcaDockablePanel(() => Rca.UI.RevitContext.CurrentUIApplication);
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Tabbed,
                TabBehind = DockablePanes.BuiltInDockablePanes.ProjectBrowser
            };
        }
    }
}