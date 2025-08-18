using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Autodesk.Revit.UI;
using RcaPlugin.Services;

namespace RcaPlugin.ViewModels
{
    /// <summary>
    /// ViewModel for the RcaDockablePanel. Handles Python code execution and UI commands.
    /// </summary>
    public class RcaDockablePanelViewModel : INotifyPropertyChanged
    {
        private string inputText;
        private string outputText;
        private readonly PythonExecutionService pythonService;

        /// <summary>
        /// Command to show hello world dialog.
        /// </summary>
        public ICommand ClickCommand { get; }
        /// <summary>
        /// Command to execute Python code.
        /// </summary>
        public ICommand ExecutePythonCommand { get; }

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

        public RcaDockablePanelViewModel(Func<UIApplication> uiappProvider)
        {
            this.uiappProvider = uiappProvider;
            pythonService = new PythonExecutionService();
            ClickCommand = new RelayCommand(OnHelloClicked);
            ExecutePythonCommand = new RelayCommand(async _ => await OnExecutePython(), _ => !string.IsNullOrWhiteSpace(InputText));
        }

        private async Task OnExecutePython()
        {
            OutputText = "Executing...";
            var uiapp = uiappProvider?.Invoke();
            if (uiapp != null)
                pythonService.SetRevitContext(uiapp);
            var result = await pythonService.ExecuteAsync(InputText);
            OutputText = result;
            InputText = string.Empty;
        }


        /// <summary>
        /// Handles the hello button click command.
        /// </summary>
        /// <param name="parameter">Command parameter (unused)</param>
        private void OnHelloClicked(object parameter)
        {
            TaskDialog.Show("RCA Plugin", "Hello, World from RCA Chat Assistant!");
        }


        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Simple RelayCommand implementation for MVVM pattern.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object> execute;
        private readonly Func<object, bool> canExecute;

        /// <summary>
        /// Initializes a new instance of the RelayCommand class.
        /// </summary>
        /// <param name="execute">The action to execute</param>
        /// <param name="canExecute">The function to determine if command can execute</param>
        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
        }

        /// <summary>
        /// Determines whether the command can execute in its current state.
        /// </summary>
        /// <param name="parameter">Command parameter</param>
        /// <returns>True if command can execute, otherwise false</returns>
        public bool CanExecute(object parameter)
        {
            return canExecute == null || canExecute(parameter);
        }

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="parameter">Command parameter</param>
        public void Execute(object parameter)
        {
            execute(parameter);
        }

        /// <summary>
        /// Occurs when changes occur that affect whether or not the command should execute.
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { System.Windows.Input.CommandManager.RequerySuggested += value; }
            remove { System.Windows.Input.CommandManager.RequerySuggested -= value; }
        }
    }
}
