using Rca.UI.ViewModels;
using System.Windows;

namespace Rca.UI.Views
{
    /// <summary>
    /// Interaction logic for DebugInfoWindow.xaml
    /// </summary>
    public partial class DebugInfoWindow : Window
    {
        public DebugInfoWindow()
        {
            InitializeComponent();
            DataContext = new DebugInfoViewModel();
        }
    }
}
