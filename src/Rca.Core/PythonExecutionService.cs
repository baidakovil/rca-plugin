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
    /// </summary>
    public class PythonExecutionService : IPythonExecutionService
    {
        private readonly ScriptEngine engine;
        private readonly ScriptScope scope;
        private UIApplication uiapp;

        // Markers and configuration
        private const string StartMarker = "--- [PYTHON EXECUTION START] ---";
        private const string EndMarker = "--- [PYTHON EXECUTION END] ---";
        private const string ErrorStartMarker = "--- [PYTHON ERROR OUTPUT START] ---";
        private const string ErrorEndMarker = "--- [PYTHON ERROR OUTPUT END] ---";
        private static readonly Encoding StdoutEncoding = Encoding.Unicode; // keep read/write consistent

        // ExternalEvent plumbing to marshal execution to Revit UI context
        private readonly ExecutePythonExternalEventHandler externalEventHandler;
        private readonly ExternalEvent externalEvent;

        /// <summary>
        /// Initializes a new instance of the <see cref="PythonExecutionService"/> class.
        /// </summary>
        public PythonExecutionService()
        {
            engine = Python.CreateEngine();
            scope = engine.CreateScope();
            engine.Runtime.LoadAssembly(typeof(Autodesk.Revit.DB.Document).Assembly); // RevitAPI.dll
            engine.Runtime.LoadAssembly(typeof(Autodesk.Revit.UI.UIDocument).Assembly); // RevitAPIUI.dll

            // Initialize ExternalEvent handler for safe Revit API access from modeless UI
            externalEventHandler = new ExecutePythonExternalEventHandler(this);
            externalEvent = ExternalEvent.Create(externalEventHandler);
        }

        /// <summary>
        /// Sets Revit API objects into the Python scope.
        /// </summary>
        public void SetRevitContext(object context)
        {
            if (context is UIApplication uiapp)
            {
                this.uiapp = uiapp;
            }
            else
            {
                throw new ArgumentException("Context must be a UIApplication instance", nameof(context));
            }
        }

        /// <summary>
        /// Injects Revit context variables into the Python scope.
        /// </summary>
        private void InjectRevitContext()
        {
            if (uiapp == null)
                throw new InvalidOperationException("Revit context not set. Call SetRevitContext() first.");

            var activeUIDoc = uiapp.ActiveUIDocument;
            if (activeUIDoc == null)
                throw new InvalidOperationException("No active document in Revit.");

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

            var tcs = new TaskCompletionSource<string>();
            externalEventHandler.Prepare(code, tcs);

            try
            {
                externalEvent.Raise();
            }
            catch (Exception ex)
            {
                // If we cannot raise ExternalEvent, format and log the error consistently
                return FormatErrorAndLog(ex.Message);
            }

            return await tcs.Task.ConfigureAwait(false);
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

            // Sanitize any NULs that some renderers display as spaces
            if (!string.IsNullOrEmpty(printOutput) && printOutput.IndexOf('\0') >= 0)
                printOutput = printOutput.Replace("\0", string.Empty);

            return (printOutput, result);
        }

        // Helper: combines captured print() output and the returned value into a single string
        private static string ComposeOutput(string printOutput, object result)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(printOutput))
            {
                sb.Append(printOutput.TrimEnd());
            }

            if (result != null)
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.Append($"Return value: {result}");
            }

            return sb.Length == 0 ? "(no output)" : sb.ToString();
        }

        // Centralized formatting + logging helpers keep execution logic clean
        private static string FormatSuccessAndLog(string output)
        {
            DebugLogService.LogPythonOutput(StartMarker);
            DebugLogService.LogPythonOutput($"Output: {output}");
            DebugLogService.LogPythonOutput(EndMarker);

            var sb = new StringBuilder();
            sb.AppendLine(StartMarker);
            sb.AppendLine($"Output: {output}");
            sb.AppendLine(EndMarker);
            return sb.ToString();
        }

        private static string FormatErrorAndLog(string errorMessage)
        {
            DebugLogService.LogError(ErrorStartMarker);
            DebugLogService.LogError($"Python Error: {errorMessage}");
            DebugLogService.LogError(ErrorEndMarker);

            var sb = new StringBuilder();
            sb.AppendLine(ErrorStartMarker);
            sb.AppendLine($"Python Error: {errorMessage}");
            sb.AppendLine(ErrorEndMarker);
            return sb.ToString();
        }

        /// <summary>
        /// ExternalEvent handler to run Python code on Revit UI context.
        /// </summary>
        private class ExecutePythonExternalEventHandler : IExternalEventHandler
        {
            private readonly PythonExecutionService service;
            private string pendingCode;
            private TaskCompletionSource<string> pendingTcs;

            public ExecutePythonExternalEventHandler(PythonExecutionService service)
            {
                this.service = service;
            }

            public void Prepare(string code, TaskCompletionSource<string> tcs)
            {
                pendingCode = code;
                pendingTcs = tcs;
            }

            public void Execute(UIApplication app)
            {
                try
                {
                    // Ensure we use the UIApplication provided by Revit at execution time
                    service.uiapp = app ?? service.uiapp;

                    service.InjectRevitContext();

                    var (printOutput, result) = service.ExecuteWithCapturedStdout(pendingCode);
                    var output = ComposeOutput(printOutput, result);

                    pendingTcs?.TrySetResult(FormatSuccessAndLog(output));
                }
                catch (Exception ex)
                {
                    pendingTcs?.TrySetResult(FormatErrorAndLog(ex.Message));
                }
                finally
                {
                    // clear state
                    pendingCode = null;
                    pendingTcs = null;
                }
            }

            public string GetName()
            {
                return "RCA Plugin - Execute Python";
            }
        }
    }
}
