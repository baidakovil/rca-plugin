using Autodesk.Revit.UI;
using Rca.Contracts.Logging;
using System;
using System.Windows.Controls;

namespace Rca.UI.ChatPanel
{
    /// <summary>
    /// Interaction logic for RcaDockablePanel.xaml
    /// </summary>
    public partial class RcaDockablePanel : UserControl
    {
        public RcaDockablePanel(Func<UIApplication> uiappProvider, ILoggingService loggingService)
        {
            InitializeComponent();
            DataContext = new RcaDockablePanelViewModel(uiappProvider, loggingService);
        }

        // Default: always resolve UIApplication and LoggingService from RevitContext
        public RcaDockablePanel() : this(() => Rca.UI.RevitContext.CurrentUIApplication, Rca.UI.RevitContext.LoggingService) { }
    }
}