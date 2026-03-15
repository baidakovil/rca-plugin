#nullable enable
using Autodesk.Revit.UI;
using Python.Runtime;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Rca.Contracts;

namespace Rca.Core.Services
{
  /// <summary>
  /// Service for executing Python code through pythonnet and providing access to Revit API objects.
  /// The service lazily initializes CPython so runtime reload can succeed even when Python is not configured yet.
  /// </summary>
  public class PythonExecutionService : IPythonExecutionService
  {
    private const string AllowThreadsFlagKey = "Rca.Core.PythonExecutionService.BeginAllowThreads";
    private const string ExecutionHelperFunctionName = "__rca_exec__";

    private static readonly object InitializationLock = new();
    private static readonly string ScopeBootstrapCode = """
import ast
import io
from contextlib import redirect_stdout

def __rca_exec__(code, scope):
    buffer = io.StringIO()
    result = None
    tree = ast.parse(code, mode='exec')
    body = list(tree.body)
    with redirect_stdout(buffer):
        if body and isinstance(body[-1], ast.Expr):
            last_expr = ast.Expression(body[-1].value)
            tree.body = body[:-1]
            if tree.body:
                exec(compile(tree, '<rca>', 'exec'), scope, scope)
            result = eval(compile(last_expr, '<rca>', 'eval'), scope, scope)
        else:
            exec(compile(tree, '<rca>', 'exec'), scope, scope)
    return buffer.getvalue(), result
""";

    private readonly object executionLock = new();
    private readonly string runtimeBaseDirectory;

    private PyModule? scope;
    private PyObject? executionHelper;
    private UIApplication? uiapp;
    private ExecutePythonExternalEventHandler? externalEventHandler;
    private ExternalEvent? externalEvent;

    /// <summary>
    /// Initializes a new instance of the <see cref="PythonExecutionService"/> class.
    /// </summary>
    public PythonExecutionService()
    {
      runtimeBaseDirectory = ResolveRuntimeBaseDirectory();
    }

    /// <summary>
    /// Gets the availability status of the required Python 3.11 runtime.
    /// </summary>
    public PythonRuntimeStatus GetRuntimeStatus()
    {
      var runtime = PythonRuntimeLocator.Locate(runtimeBaseDirectory);
      return runtime.IsAvailable
          ? PythonRuntimeStatus.Available(runtime.PythonDllPath!)
          : PythonRuntimeStatus.MissingInstallation(runtime.FailureReason ?? $"Python {PythonRuntimeStatus.SupportedVersion} is required to execute scripts.");
    }

    /// <summary>
    /// Sets Revit API objects into the Python scope.
    /// </summary>
    public void SetRevitContext(object context)
    {
      if (context is UIApplication uiapp) this.uiapp = uiapp; else throw new ArgumentException("Context must be a UIApplication instance", nameof(context));
    }

    /// <summary>
    /// Initializes the ExternalEvent if it hasn't been created yet.
    /// This is done lazily to avoid issues when instantiating outside of Revit API context.
    /// </summary>
    private void EnsureExternalEventInitialized()
    {
      if (externalEvent == null || externalEventHandler == null)
      {
        externalEventHandler = new ExecutePythonExternalEventHandler(this);
        externalEvent = ExternalEvent.Create(externalEventHandler);
      }
    }

    /// <summary>
    /// Injects Revit context variables into the Python scope.
    /// </summary>
    private void InjectRevitContext()
    {
      if (uiapp == null) throw new InvalidOperationException("Revit context not set. Call SetRevitContext() first.");
      if (scope == null) throw new InvalidOperationException("Python scope not initialized.");

      var activeUIDoc = uiapp.ActiveUIDocument ?? throw new InvalidOperationException("No active document in Revit.");
      scope.Set("uiapp", uiapp);
      scope.Set("uidoc", activeUIDoc);
      scope.Set("doc", activeUIDoc.Document);
    }

    /// <summary>
    /// Executes the given Python code asynchronously via Revit ExternalEvent (UI context).
    /// </summary>
    /// <param name="code">Python code to execute.</param>
    /// <returns>Result of execution or exception message.</returns>
    public async Task<string> ExecuteAsync(string code)
    {
      if (string.IsNullOrWhiteSpace(code)) return string.Empty;
      EnsureExternalEventInitialized();
      var tcs = new TaskCompletionSource<string>();
      externalEventHandler!.Prepare(code, tcs);
      try { externalEvent!.Raise(); } catch (Exception ex) { return FormatError(GetReadableErrorMessage(ex)); }
      return await tcs.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Executes Python code synchronously. This method should only be called from within a Revit API context.
    /// </summary>
    /// <param name="code">Python code to execute.</param>
    /// <returns>Result of execution or exception message.</returns>
    public string ExecuteSync(string code)
    {
      if (string.IsNullOrWhiteSpace(code)) return string.Empty;
      try
      {
        lock (executionLock)
        {
          EnsureScopeInitialized();
          if (uiapp != null) InjectRevitContext();

          var (printOutput, result) = ExecuteWithCapturedStdout(code);
          var output = ComposeOutput(printOutput, result);
          return FormatSuccess(output);
        }
      }
      catch (Exception ex) { return FormatError(GetReadableErrorMessage(ex)); }
    }

    private void EnsureScopeInitialized()
    {
      EnsurePythonRuntimeInitialized();
      if (scope != null && executionHelper != null)
      {
        return;
      }

      using (Py.GIL())
      {
        if (scope != null && executionHelper != null)
        {
          return;
        }

        scope = Py.CreateScope();
        ConfigureScope(scope);
        scope.Exec(ScopeBootstrapCode);
        executionHelper = scope.Get(ExecutionHelperFunctionName);
      }
    }

    private void EnsurePythonRuntimeInitialized()
    {
      lock (InitializationLock)
      {
        var runtime = PythonRuntimeLocator.Locate(runtimeBaseDirectory);
        if (!runtime.IsAvailable || string.IsNullOrWhiteSpace(runtime.PythonDllPath) || string.IsNullOrWhiteSpace(runtime.PythonHome))
        {
          throw new InvalidOperationException(runtime.FailureReason ?? "PythonNet runtime not configured.");
        }

        Environment.SetEnvironmentVariable("PYTHONNET_PYDLL", runtime.PythonDllPath);
        Environment.SetEnvironmentVariable("PYTHONHOME", runtime.PythonHome);
        if (!string.IsNullOrWhiteSpace(runtime.PythonPath))
        {
          Environment.SetEnvironmentVariable("PYTHONPATH", runtime.PythonPath);
        }

        Python.Runtime.Runtime.PythonDLL = runtime.PythonDllPath;
        PythonEngine.Initialize();

        if (!(AppDomain.CurrentDomain.GetData(AllowThreadsFlagKey) is bool allowThreadsReleased && allowThreadsReleased))
        {
          PythonEngine.BeginAllowThreads();
          AppDomain.CurrentDomain.SetData(AllowThreadsFlagKey, true);
        }
      }
    }

    private void ConfigureScope(PyModule pythonScope)
    {
      pythonScope.Set("__rca_search_paths__", BuildScopeSearchPaths());
      pythonScope.Exec(
          "import sys\n" +
          "for _rca_path in __rca_search_paths__:\n" +
          "    if _rca_path and _rca_path not in sys.path:\n" +
          "        sys.path.insert(0, _rca_path)\n" +
          "globals().pop('_rca_path', None)\n");
      pythonScope.Remove("__rca_search_paths__");

      pythonScope.Exec(
          "try:\n" +
          "    import clr\n" +
          "    for _rca_ref in ('RevitAPI', 'RevitAPIUI', 'Rca.Contracts', 'Rca.Core'):\n" +
          "        try:\n" +
          "            clr.AddReference(_rca_ref)\n" +
          "        except Exception:\n" +
          "            pass\n" +
          "    globals().pop('_rca_ref', None)\n" +
          "except Exception:\n" +
          "    pass\n");
    }

    private string[] BuildScopeSearchPaths()
    {
      var pythonRuntime = PythonRuntimeLocator.Locate(runtimeBaseDirectory);
      var searchPaths = new System.Collections.Generic.List<string>();

      AddPathIfPresent(searchPaths, runtimeBaseDirectory);
      AddPathIfPresent(searchPaths, AppContext.BaseDirectory);
      AddPathIfPresent(searchPaths, Path.GetDirectoryName(typeof(Document).Assembly.Location));
      AddPathIfPresent(searchPaths, Path.GetDirectoryName(typeof(UIDocument).Assembly.Location));

      foreach (var searchPath in pythonRuntime.SearchPaths)
      {
        AddPathIfPresent(searchPaths, searchPath);
      }

      return searchPaths.ToArray();
    }

    private static void AddPathIfPresent(System.Collections.Generic.ICollection<string> paths, string? path)
    {
      if (string.IsNullOrWhiteSpace(path))
      {
        return;
      }

      var normalizedPath = path.Trim();
      if (!Directory.Exists(normalizedPath) && !File.Exists(normalizedPath))
      {
        return;
      }

      if (!paths.Contains(normalizedPath))
      {
        paths.Add(normalizedPath);
      }
    }

    private static string ResolveRuntimeBaseDirectory()
    {
      try
      {
        return Path.GetDirectoryName(typeof(PythonExecutionService).Assembly.Location) ?? AppContext.BaseDirectory;
      }
      catch
      {
        return AppContext.BaseDirectory;
      }
    }

    // Helper: executes code and captures print() output without touching Python scope (synchronous, runs on Revit UI thread)
    private (string printOutput, object? result) ExecuteWithCapturedStdout(string code)
    {
      if (scope == null || executionHelper == null)
      {
        throw new InvalidOperationException("Python scope not initialized.");
      }

      using var gil = Py.GIL();
      using var codeObject = new PyString(code);
      using var scopeVariables = scope.Variables();
      using var invocationResult = executionHelper.Invoke(new PyObject[] { codeObject, scopeVariables });
      using var pythonResultTuple = new PyTuple(invocationResult);
      using var stdoutValue = pythonResultTuple[0];
      using var returnValue = pythonResultTuple[1];

      var printOutput = stdoutValue.As<string?>() ?? string.Empty;
      var managedResult = ConvertPythonResult(returnValue);
      return (printOutput, managedResult);
    }

    private static object? ConvertPythonResult(PyObject pythonValue)
    {
      if (pythonValue.IsNone())
      {
        return null;
      }

      try
      {
        return pythonValue.AsManagedObject(typeof(object)) ?? pythonValue.ToString();
      }
      catch
      {
        return pythonValue.ToString();
      }
    }

    // Helper: combines captured print() output and the returned value into a single string
    private static string ComposeOutput(string printOutput, object? result)
    {
      var sb = new StringBuilder();
      if (!string.IsNullOrWhiteSpace(printOutput)) sb.Append(printOutput.TrimEnd());
      if (result != null)
      {
        if (sb.Length > 0) sb.AppendLine();
        sb.Append($"Return value: {result}");
      }
      return sb.Length == 0 ? "(no output)" : sb.ToString();
    }

    // Centralized formatting + logging helpers keep execution logic clean
    private static string FormatSuccess(string output) => output;
    private static string FormatError(string errorMessage) => $"Python Error: {errorMessage}";
    private static string GetReadableErrorMessage(Exception exception) => exception.Message;

    /// <summary>
    /// ExternalEvent handler to run Python code on Revit UI context.
    /// </summary>
    private class ExecutePythonExternalEventHandler : IExternalEventHandler
    {
      private readonly PythonExecutionService service;
      private string? pendingCode;
      private TaskCompletionSource<string>? pendingTcs;
      public ExecutePythonExternalEventHandler(PythonExecutionService service) => this.service = service;
      public void Prepare(string code, TaskCompletionSource<string> tcs) { pendingCode = code; pendingTcs = tcs; }
      public void Execute(UIApplication app)
      {
        try
        {
          service.uiapp = app ?? service.uiapp;
          service.InjectRevitContext();
          var (printOutput, result) = service.ExecuteWithCapturedStdout(pendingCode!);
          var output = ComposeOutput(printOutput, result);
          pendingTcs?.TrySetResult(PythonExecutionService.FormatSuccess(output));
        }
        catch (Exception ex) { pendingTcs?.TrySetResult(PythonExecutionService.FormatError(ex.Message)); }
        finally { pendingCode = null; pendingTcs = null; }
      }
      public string GetName() => "RCA Plugin - Execute Python";
    }
  }
}
