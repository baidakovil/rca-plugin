using Autodesk.Revit.UI;
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
        public RcaDockablePanel(Func<UIApplication> uiappProvider)
        {
            InitializeComponent();
            DataContext = new RcaDockablePanelViewModel(uiappProvider);
        }

        // Default: always resolve UIApplication from RevitContext
        public RcaDockablePanel() : this(() => Rca.Core.RevitContext.CurrentUIApplication) { }
    }
}