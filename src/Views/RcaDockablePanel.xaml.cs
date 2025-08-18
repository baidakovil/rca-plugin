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
        public RcaDockablePanel(UIApplication uiapp = null)
        {
            InitializeComponent();
            DataContext = new RcaDockablePanelViewModel(uiapp);
        }
    }
}