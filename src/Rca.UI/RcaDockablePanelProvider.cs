using Autodesk.Revit.UI;

namespace Rca.UI.Views
{
    public class RcaDockablePanelProvider : IDockablePaneProvider
    {
        public RcaDockablePanelProvider() { }

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            // Always resolve UIApplication at runtime from RevitContext
            data.FrameworkElement = new RcaDockablePanel(() => Rca.Core.RevitContext.CurrentUIApplication);
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Tabbed,
                TabBehind = DockablePanes.BuiltInDockablePanes.ProjectBrowser
            };
        }
    }
}