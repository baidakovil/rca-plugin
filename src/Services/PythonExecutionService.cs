using Autodesk.Revit.UI;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace RcaPlugin.Services
{
    /// <summary>
    /// Service for executing Python code using IronPython3 and providing access to Revit API objects.
    /// </summary>
    public class PythonExecutionService
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

        /// <summary>
        /// Initializes a new instance of the <see cref="PythonExecutionService"/> class.
        /// </summary>
        public PythonExecutionService()
        {
            engine = Python.CreateEngine();
            scope = engine.CreateScope();
            engine.Runtime.LoadAssembly(typeof(Autodesk.Revit.DB.Document).Assembly); // RevitAPI.dll
            engine.Runtime.LoadAssembly(typeof(Autodesk.Revit.UI.UIDocument).Assembly); // RevitAPIUI.dll
        }

        /// <summary>
        /// Sets Revit API objects into the Python scope.
        /// </summary>
        public void SetRevitContext(UIApplication uiapp)
        {
            if (uiapp == null) throw new ArgumentNullException(nameof(uiapp));
            this.uiapp = uiapp;
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
        /// Executes the given Python code asynchronously.
        /// </summary>
        /// <param name="code">Python code to execute.</param>
        /// <returns>Result of execution or exception message.</returns>
        public async Task<string> ExecuteAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return string.Empty;
            var sb = new StringBuilder();
            DebugLogService.LogPythonOutput(StartMarker);
            sb.AppendLine(StartMarker);
            try
            {
                InjectRevitContext();

                var (printOutput, result) = await ExecuteWithCapturedStdoutAsync(code);
                var output = ComposeOutput(printOutput, result);

                DebugLogService.LogPythonOutput($"Output: {output}");
                sb.AppendLine($"Output: {output}");
                DebugLogService.LogPythonOutput(EndMarker);
                sb.AppendLine(EndMarker);
                return sb.ToString();
            }
            catch (Exception ex)
            {
                DebugLogService.LogError(ErrorStartMarker);
                sb.AppendLine(ErrorStartMarker);
                DebugLogService.LogError($"Python Error: {ex.Message}");
                sb.AppendLine($"Python Error: {ex.Message}");
                DebugLogService.LogError(ErrorEndMarker);
                sb.AppendLine(ErrorEndMarker);
                return sb.ToString();
            }
        }

        // Helper: executes code and captures print() output without touching Python scope
        private async Task<(string printOutput, object result)> ExecuteWithCapturedStdoutAsync(string code)
        {
            using var outputStream = new MemoryStream();
            engine.Runtime.IO.SetOutput(outputStream, StdoutEncoding);

            var source = engine.CreateScriptSourceFromString(code);
            var result = await Task.Run(() => source.Execute(scope));

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
    }
}
