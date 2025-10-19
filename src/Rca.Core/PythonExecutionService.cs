#nullable enable
using Autodesk.Revit.UI;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Rca.Contracts;

namespace Rca.Core.Services
{
    /// <summary>
    /// Service for executing Python code using IronPython3 and providing access to Revit API objects.
    /// Legacy debug log integration removed; caller must log externally via new logging system.
    /// </summary>
    public class PythonExecutionService : IPythonExecutionService
    {
        private readonly ScriptEngine engine;
        private readonly ScriptScope scope;
        private UIApplication? uiapp;

        private static readonly Encoding StdoutEncoding = Encoding.Unicode;
        private ExecutePythonExternalEventHandler? externalEventHandler;
        private ExternalEvent? externalEvent;

        /// <summary>
        /// Initializes a new instance of the <see cref="PythonExecutionService"/> class.
        /// </summary>
        public PythonExecutionService()
        {
            engine = Python.CreateEngine();
            scope = engine.CreateScope();
            engine.Runtime.LoadAssembly(typeof(Document).Assembly);
            engine.Runtime.LoadAssembly(typeof(UIDocument).Assembly);
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
            var activeUIDoc = uiapp.ActiveUIDocument ?? throw new InvalidOperationException("No active document in Revit.");
            AppDomain.CurrentDomain.SetData("uiapp", uiapp);
            AppDomain.CurrentDomain.SetData("uidoc", activeUIDoc);
            AppDomain.CurrentDomain.SetData("doc", activeUIDoc.Document);
            scope.SetVariable("uiapp", uiapp);
            scope.SetVariable("uidoc", activeUIDoc);
            scope.SetVariable("doc", activeUIDoc.Document);
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
            try { externalEvent!.Raise(); } catch (Exception ex) { return FormatError(ex.Message); }
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
                if (uiapp != null) InjectRevitContext();
                var (printOutput, result) = ExecuteWithCapturedStdout(code);
                var output = ComposeOutput(printOutput, result);
                return FormatSuccess(output);
            }
            catch (Exception ex) { return FormatError(ex.Message); }
        }

        // Helper: executes code and captures print() output without touching Python scope (synchronous, runs on Revit UI thread)
        private (string printOutput, object result) ExecuteWithCapturedStdout(string code)
        {
            using var outputStream = new MemoryStream();
            engine.Runtime.IO.SetOutput(outputStream, StdoutEncoding);
            var source = engine.CreateScriptSourceFromString(code);
            var result = source.Execute(scope);
            outputStream.Position = 0;
            using var reader = new StreamReader(outputStream, StdoutEncoding, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            var printOutput = reader.ReadToEnd();
            if (!string.IsNullOrEmpty(printOutput) && printOutput.IndexOf('\0') >= 0) printOutput = printOutput.Replace("\0", string.Empty);
            return (printOutput, result);
        }

        // Helper: combines captured print() output and the returned value into a single string
        private static string ComposeOutput(string printOutput, object result)
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
