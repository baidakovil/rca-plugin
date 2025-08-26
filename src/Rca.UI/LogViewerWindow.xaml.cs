using Rca.Contracts.Logging;
using Rca.UI.LogWindow;
using System;
using System.Windows;

namespace Rca.UI
{
    /// <summary>
    /// Interaction logic for LogViewerWindow.xaml
    /// </summary>
    public partial class LogViewerWindow : Window
    {
        /// <summary>
        /// Initializes a new instance of the LogViewerWindow class.
        /// </summary>
        /// <param name="loggingService">The logging service to use for displaying logs.</param>
        public LogViewerWindow(ILoggingService loggingService)
        {
            if (loggingService == null)
                throw new ArgumentNullException(nameof(loggingService));
                
            InitializeComponent();
            DataContext = new LogViewerViewModel(loggingService);
        }
    }
}