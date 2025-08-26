using Rca.Contracts.Logging;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Rca.UI.LogWindow
{
    /// <summary>
    /// ViewModel for the log viewer window, exposes log entries and commands.
    /// </summary>
    public class LogViewerViewModel
    {
        private readonly ILoggingService _loggingService;

        /// <summary>
        /// Initializes a new instance of the LogViewerViewModel class with a specific logging service.
        /// </summary>
        /// <param name="loggingService">The logging service to use.</param>
        public LogViewerViewModel(ILoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            ClearLogsCommand = new RelayCommand(ClearLogs);
        }

        /// <summary>
        /// Gets the collection of log entries.
        /// </summary>
        public ReadOnlyObservableCollection<LogEntry> LogEntries => _loggingService.Entries;

        /// <summary>
        /// Gets the command to clear all log entries.
        /// </summary>
        public ICommand ClearLogsCommand { get; }

        private void ClearLogs()
        {
            _loggingService.Clear();
        }
    }

    /// <summary>
    /// Simple relay command implementation for MVVM pattern.
    /// </summary>
    internal class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => _execute();
    }
}