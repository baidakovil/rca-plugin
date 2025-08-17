using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.UI;
using RcaPlugin.ViewModels;

namespace RcaPlugin.Views
{
    /// <summary>
    /// Interaction logic for RcaDockablePanel.xaml
    /// </summary>
    public partial class RcaDockablePanel : UserControl
    {
        public RcaDockablePanel()
        {
            InitializeComponent();
            DataContext = new RcaDockablePanelViewModel();
        }
    }
}