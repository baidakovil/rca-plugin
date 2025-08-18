using Autodesk.Revit.UI;

namespace RcaPlugin.Views
{
    public class RcaDockablePanelProvider : IDockablePaneProvider
    {
        private readonly UIApplication uiapp;

        public RcaDockablePanelProvider(UIApplication uiapp)
        {
            this.uiapp = uiapp;
        }

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            data.FrameworkElement = new RcaDockablePanel(() => uiapp);
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Tabbed,
                TabBehind = DockablePanes.BuiltInDockablePanes.ProjectBrowser
            };
        }
    }
}