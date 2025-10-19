using System;
using Autodesk.Revit.UI;
using Rca.Loader.Contracts;

namespace Rca.Loader.UI
{
    /// <summary>
    /// Dockable pane provider that returns the <see cref="DockablePanelHost"/> for registration with Revit.
    /// </summary>
    public class DockablePanelProvider : IDockablePaneProvider
    {
        private readonly DockablePanelHost host;

        /// <summary>
        /// Initializes a new instance of the <see cref="DockablePanelProvider"/> class.
        /// </summary>
        /// <param name="host">The host control instance to provide.</param>
        public DockablePanelProvider(DockablePanelHost host)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            data.FrameworkElement = host;
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Tabbed,
                TabBehind = DockablePanes.BuiltInDockablePanes.ProjectBrowser
            };
        }
    }
}
