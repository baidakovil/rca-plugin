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

        private void OnHelloClicked(object parameter)
        {
            TaskDialog.Show("Hello", "hello, world");
        }
    }

    /// <summary>
    /// Simple RelayCommand implementation for MVVM.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object> execute;
        private readonly Func<object, bool> canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return canExecute == null || canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            execute(parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
