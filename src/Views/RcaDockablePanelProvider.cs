using Autodesk.Revit.UI;

namespace RcaPlugin.Views
{
    public class RcaDockablePanelProvider : IDockablePaneProvider
    {
        public void SetupDockablePane(DockablePaneProviderData data)
        {
            data.FrameworkElement = new RcaDockablePanel();
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Tabbed,
                TabBehind = DockablePanes.BuiltInDockablePanes.ProjectBrowser
            };
        }
    }
}