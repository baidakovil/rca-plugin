using Autodesk.Revit.UI;
using System;
using Rca.Contracts;

namespace Rca.UI.Views
{
  /// <summary>
  /// Provides the dockable panel. Debug logging UI removed in favor of unified logging system.
  /// </summary>
  public class RcaDockablePanelProvider : IDockablePaneProvider
  {
    private readonly Func<UIApplication> uiappProvider;
    private readonly IPythonExecutionService pythonService;

    public RcaDockablePanelProvider(
        Func<UIApplication> uiappProvider,
        IPythonExecutionService pythonService)
    {
      this.uiappProvider = uiappProvider;
      this.pythonService = pythonService;
    }

    public void SetupDockablePane(DockablePaneProviderData data)
    {
      if (data is null)
        throw new ArgumentNullException(nameof(data));

      data.FrameworkElement = new RcaDockablePanel(() => uiappProvider(), pythonService);
      data.InitialState = new DockablePaneState
      {
        DockPosition = DockPosition.Tabbed,
        TabBehind = DockablePanes.BuiltInDockablePanes.ProjectBrowser
      };
    }
  }
}
