using Autodesk.Revit.UI;
using Rca.Contracts;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Rca.UI.ViewModels
{
  /// <summary>
  /// ViewModel for the RcaDockablePanel. Handles Python code execution and UI commands.
  /// Debug log window removed.
  /// </summary>
  public class RcaDockablePanelViewModel : INotifyPropertyChanged
  {
    private string inputText = string.Empty;
    private string outputText = string.Empty;
    private readonly IPythonExecutionService pythonService;
    private readonly Action<PythonRuntimeStatus> showPythonInstallPrompt;
    // NOTE: Use object to avoid hard runtime dependency on RevitAPIUI in unit tests
    private readonly Func<object?> revitContextProvider;

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
      set { inputText = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
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
    public RcaDockablePanelViewModel(
        Func<object?> revitContextProvider,
        IPythonExecutionService pythonService,
        Action<PythonRuntimeStatus>? showPythonInstallPrompt = null)
    {
      this.revitContextProvider = revitContextProvider;
      this.pythonService = pythonService;
      this.showPythonInstallPrompt = showPythonInstallPrompt ?? ShowPythonInstallPrompt;
      ClickCommand = new RelayCommand(OnHelloClicked);
      ExecutePythonCommand = new RelayCommand(async _ => await OnExecutePython(), _ => !string.IsNullOrWhiteSpace(InputText));
    }

    private async Task OnExecutePython()
    {
      var runtimeStatus = pythonService.GetRuntimeStatus();
      if (!runtimeStatus.IsAvailable)
      {
        OutputText = runtimeStatus.Message;
        if (!string.IsNullOrWhiteSpace(runtimeStatus.DownloadUrl))
          showPythonInstallPrompt(runtimeStatus);
        return;
      }

      OutputText = "Executing...";

      try
      {
        var context = revitContextProvider?.Invoke();
        if (context != null)
          pythonService.SetRevitContext(context);

        // Use ExecuteSync in Task.Run for dockable panels since they already run in Revit context
        // ExecuteAsync would try to create ExternalEvent which is not allowed in dockable panel context
        var result = await Task.Run(() => pythonService.ExecuteSync(InputText));
        OutputText = result;
        InputText = string.Empty;
      }
      catch (Exception ex)
      {
        OutputText = $"Error: {ex.Message}";
      }
    }

    private static void ShowPythonInstallPrompt(PythonRuntimeStatus runtimeStatus)
    {
      var taskDialog = new TaskDialog($"Python {PythonRuntimeStatus.SupportedVersion} Required")
      {
        MainIcon = TaskDialogIcon.TaskDialogIconWarning,
        MainInstruction = $"Python {PythonRuntimeStatus.SupportedVersion} is required to run scripts.",
        MainContent = runtimeStatus.Message + "\n\nRCA includes pythonnet, but CPython itself must be installed separately by the user. Install Python 3.11 (64-bit), then restart Revit.",
        CommonButtons = TaskDialogCommonButtons.Cancel,
        DefaultButton = TaskDialogResult.Cancel
      };

      taskDialog.AddCommandLink(
          TaskDialogCommandLinkId.CommandLink1,
          "Open the official Python download page",
          runtimeStatus.DownloadUrl ?? PythonRuntimeStatus.OfficialDownloadUrl);

      var result = taskDialog.Show();
      if (result == TaskDialogResult.CommandLink1)
      {
        OpenBrowser(runtimeStatus.DownloadUrl ?? PythonRuntimeStatus.OfficialDownloadUrl);
      }
    }

    private static void OpenBrowser(string url)
    {
      try
      {
        using var process = Process.Start(new ProcessStartInfo(url)
        {
          UseShellExecute = true
        });
      }
      catch (Exception ex)
      {
        TaskDialog.Show($"Python {PythonRuntimeStatus.SupportedVersion} Required", $"Open this URL manually:\n{url}\n\n{ex.Message}");
      }
    }


    /// <summary>
    /// Handles the hello button click command.
    /// /// </summary>
    /// <param name="parameter">Command parameter (unused)</param>
    private void OnHelloClicked(object parameter)
    {
      TaskDialog.Show("RCA Plugin", "Hello, World from RCA Chat Assistant!");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
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
    private readonly Func<object, bool>? canExecute;

    /// <summary>
    /// Initializes a new instance of the RelayCommand class.
    /// </summary>
    /// <param name="execute">The action to execute</param>
    /// <param name="canExecute">The function to determine if command can execute</param>
    public RelayCommand(Action<object> execute, Func<object, bool>? canExecute = null)
    {
      this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
      this.canExecute = canExecute;
    }

    /// <summary>
    /// Determines whether the command can execute in its current state.
    /// </summary>
    /// <param name="parameter">Command parameter</param>
    /// <returns>True if command can execute, otherwise false</returns>
    public bool CanExecute(object? parameter)
    {
      return canExecute == null || canExecute(parameter!);
    }

    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="parameter">Command parameter</param>
    public void Execute(object? parameter)
    {
      execute(parameter!);
    }

    /// <summary>
    /// Occurs when changes occur that affect whether or not the command should execute.
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
      add { System.Windows.Input.CommandManager.RequerySuggested += value; }
      remove { System.Windows.Input.CommandManager.RequerySuggested -= value; }
    }
  }
}
