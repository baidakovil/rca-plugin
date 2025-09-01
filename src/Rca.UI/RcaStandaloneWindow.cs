using Rca.Contracts;
using Rca.Contracts.Infrastructure;
#if !LINUX_BUILD
using System.Windows;
#endif

namespace Rca.UI.Views
{
    /// <summary>
    /// Window for displaying RcaDockablePanel outside of dockable panel in Revit.
    /// </summary>
#if !LINUX_BUILD
    public class RcaStandaloneWindow : Window
#else
    public class RcaStandaloneWindow
#endif
    {
        public RcaStandaloneWindow()
        {
#if !LINUX_BUILD
            Title = "RCA Chat Assistant (Standalone)";
            Width = 400;
            Height = 600;
            
            // Use dependency injection to create the content
            var container = ServiceContainer.Instance;
            var revitContext = container.Resolve<IRevitContext>();
            var pythonService = container.Resolve<IPythonExecutionService>();
            var debugLogService = container.Resolve<IDebugLogService>();
            
            Content = new RcaDockablePanel(
                () => revitContext.CurrentUIApplication as Autodesk.Revit.UI.UIApplication,
                pythonService,
                () => new DebugInfoWindow(debugLogService));
#endif
        }
    }
}
