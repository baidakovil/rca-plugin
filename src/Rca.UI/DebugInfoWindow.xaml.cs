using Rca.Contracts;
using Rca.UI.ViewModels;
#if !LINUX_BUILD
using System.Windows;
#endif

namespace Rca.UI.Views
{
    /// <summary>
    /// Interaction logic for DebugInfoWindow.xaml
    /// </summary>
#if !LINUX_BUILD
    public partial class DebugInfoWindow : Window
#else
    public class DebugInfoWindow
#endif
    {
        public DebugInfoWindow(IDebugLogService debugLogService)
        {
#if !LINUX_BUILD
            InitializeComponent();
            DataContext = new DebugInfoViewModel(debugLogService);
#endif
        }
    }
}
