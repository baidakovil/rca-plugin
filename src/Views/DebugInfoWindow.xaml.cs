using RcaPlugin.ViewModels;
using System.Windows;

namespace RcaPlugin.Views
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
