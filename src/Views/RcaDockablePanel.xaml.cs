using Autodesk.Revit.UI;
using RcaPlugin.ViewModels;
using System;
using System.Windows.Controls;

namespace RcaPlugin.Views
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
        public RcaDockablePanel() : this(() => RcaPlugin.RevitContext.CurrentUIApplication) { }
    }
}