using Autodesk.Revit.UI;
using Rca.Contracts.Logging;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Rca.UI.ChatPanel
{
    /// <summary>
    /// ViewModel for the RcaDockablePanel. Handles Python code execution and UI commands.
    /// </summary>
    public class RcaDockablePanelViewModel : INotifyPropertyChanged
    {
        private string inputText;
        private string outputText;
        private readonly PythonExecutionService pythonService;
        private readonly ILoggingService loggingService;

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Command to show hello world dialog.
        /// </summary>
        public ICommand ClickCommand { get; }
        /// <summary>
        /// Command to execute Python code.
        /// </summary>
        public ICommand ExecutePythonCommand { get; }
        /// <summary>
        /// Command to show log viewer.
        /// </summary>
        public ICommand ShowLogViewerCommand { get; }

        /// <summary>
        /// The Python code input by the user.
        /// </summary>
        public string InputText
        {
            get => inputText;
            set { inputText = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// The output/result of Python code execution.
        /// </summary>
        public string OutputText
        {
            get => outputText;
            set { outputText = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Initializes a new instance of the RcaDockablePanelViewModel class.
        /// </summary>
        private readonly Func<UIApplication> uiappProvider;

        public RcaDockablePanelViewModel(Func<UIApplication> uiappProvider, ILoggingService loggingService)
        {
            this.uiappProvider = uiappProvider ?? throw new ArgumentNullException(nameof(uiappProvider));
            this.loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));

            pythonService = new PythonExecutionService(loggingService);
            ClickCommand = new RelayCommand(OnHelloClicked);
            ExecutePythonCommand = new RelayCommand(async _ => await OnExecutePython(), _ => !string.IsNullOrWhiteSpace(InputText));
            ShowLogViewerCommand = new RelayCommand(_ => OnShowLogViewer());
        }

        private async Task OnExecutePython()
        {
            OutputText = "Executing...";
        }

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">Name of the property that changed.</param>
        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}