using Autodesk.Revit.UI;
using Rca.Contracts;
using Rca.UI.ViewModels;
using System;
using System.Windows.Controls;

namespace Rca.UI.Views
{
    /// <summary>
    /// Interaction logic for RcaDockablePanel.xaml
    /// </summary>
    public partial class RcaDockablePanel : UserControl
    {
        public RcaDockablePanel(
            Func<UIApplication> uiappProvider, 
            IPythonExecutionService pythonService,
            Func<DebugInfoWindow> debugInfoWindowFactory)
        {
            InitializeComponent();
            DataContext = new RcaDockablePanelViewModel(uiappProvider, pythonService, debugInfoWindowFactory);
        }
    }
}