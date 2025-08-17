using System;
using System.Windows.Input;
using Autodesk.Revit.UI;

namespace RcaPlugin.ViewModels
{
    /// <summary>
    /// ViewModel for the RcaDockablePanel. Handles the Hello button command.
    /// </summary>
    public class RcaDockablePanelViewModel
    {
        /// <summary>
        /// Command to show hello world dialog.
        /// </summary>
        public ICommand ClickCommand { get; }

        /// <summary>
        /// Initializes a new instance of the RcaDockablePanelViewModel class.
        /// </summary>
        public RcaDockablePanelViewModel()
        {
            ClickCommand = new RelayCommand(OnHelloClicked);
        }

        /// <summary>
        /// Handles the hello button click command.
        /// </summary>
        /// <param name="parameter">Command parameter (unused)</param>
        private void OnHelloClicked(object parameter)
        {
            TaskDialog.Show("RCA Plugin", "Hello, World from RCA Chat Assistant!");
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
