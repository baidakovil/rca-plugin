using Autodesk.Revit.UI;
using Rca.Contracts;
using Rca.UI.ViewModels;
using System;
#if !LINUX_BUILD
using System.Windows.Controls;
#endif

namespace Rca.UI.Views
{
    /// <summary>
    /// Interaction logic for RcaDockablePanel.xaml
    /// </summary>
#if !LINUX_BUILD
    public partial class RcaDockablePanel : UserControl
#else
    public class RcaDockablePanel
#endif
    {
        public RcaDockablePanel(
            Func<UIApplication> uiappProvider, 
            IPythonExecutionService pythonService,
            Func<DebugInfoWindow> debugInfoWindowFactory)
        {
#if !LINUX_BUILD
            InitializeComponent();
            DataContext = new RcaDockablePanelViewModel(uiappProvider, pythonService, debugInfoWindowFactory);
#endif
        }
    }
}