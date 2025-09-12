using Autodesk.Revit.UI;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Rca.Contracts;
using Autodesk.Revit.UI.Events;

namespace Rca.Core.Services
{
    /// <summary>
    /// Service for executing Python code using IronPython3 and providing access to Revit API objects.
    /// </summary>
    public class PythonExecutionService : IPythonExecutionService
    {
        private readonly ScriptEngine engine;
        private readonly ScriptScope scope;
        private UIApplication? uiapp;

        // Markers and configuration
        private const string StartMarker = "--- [PYTHON EXECUTION START] ---";
        private const string EndMarker = "--- [PYTHON EXECUTION END] ---";
        private const string ErrorStartMarker = "--- [PYTHON ERROR OUTPUT START] ---";
        private const string ErrorEndMarker = "--- [PYTHON ERROR OUTPUT END] ---";
        private static readonly Encoding StdoutEncoding = Encoding.Unicode; // keep read/write consistent

        // ExternalEvent plumbing to marshal execution to Revit UI context (lazy initialization)
        private ExecutePythonExternalEventHandler? externalEventHandler;
        private ExternalEvent? externalEvent;

        /// <summary>
        /// Initializes a new instance of the <see cref="PythonExecutionService"/> class.
        /// </summary>
        public PythonExecutionService()
        {
            engine = Python.CreateEngine();
            scope = engine.CreateScope();
            engine.Runtime.LoadAssembly(typeof(Autodesk.Revit.DB.Document).Assembly); // RevitAPI.dll
            engine.Runtime.LoadAssembly(typeof(Autodesk.Revit.UI.UIDocument).Assembly); // RevitAPIUI.dll

            // Don't create ExternalEvent here - it will be created lazily when needed
            // This allows the service to be instantiated in test contexts
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
        /// Initializes the ExternalEvent if it hasn't been created yet.
        /// This is done lazily to avoid issues when instantiating outside of Revit API context.
        /// </summary>
        private void EnsureExternalEventInitialized()
        {
            if (externalEvent == null || externalEventHandler == null)
            {
                try
                {
                    // Initialize ExternalEvent handler for safe Revit API access from modeless UI
                    externalEventHandler = new ExecutePythonExternalEventHandler(this);
                    externalEvent = ExternalEvent.Create(externalEventHandler);
                    
                    // Log successful initialization for debugging
                    DebugLogService.StaticLogInfo("ExternalEvent initialized successfully for Python execution");
                }
                catch (Autodesk.Revit.Exceptions.InvalidOperationException ex)
                {
                    var errorMsg = "Cannot create ExternalEvent outside of Revit API context. " +
                                  "This service can only execute Python code when called from within Revit.";
                    DebugLogService.StaticLogError($"ExternalEvent initialization failed: {ex.Message}");
                    throw new InvalidOperationException(errorMsg, ex);
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Unexpected error initializing ExternalEvent: {ex.Message}";
                    DebugLogService.StaticLogError(errorMsg);
                    throw new InvalidOperationException(errorMsg, ex);
                }
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

            try
            {
                // Ensure ExternalEvent is initialized
                EnsureExternalEventInitialized();

                DebugLogService.StaticLogInfo($"ExecuteAsync: Starting Python execution via ExternalEvent");

                var tcs = new TaskCompletionSource<string>();
                externalEventHandler!.Prepare(code, tcs);

                // Check if ExternalEvent is in a valid state
                if (externalEvent == null)
                {
                    const string errorMsg = "ExternalEvent is null after initialization";
                    DebugLogService.StaticLogError(errorMsg);
                    return FormatErrorAndLog(errorMsg);
                }

                try
                {
                    var raiseResult = externalEvent.Raise();
                    DebugLogService.StaticLogInfo($"ExternalEvent.Raise() result: {raiseResult}");
                    
                    if (raiseResult != ExternalEventRequest.Accepted)
                    {
                        var errorMsg = $"ExternalEvent.Raise() was not accepted. Result: {raiseResult}. " +
                                      "This usually indicates that Revit is busy or the request cannot be processed.";
                        DebugLogService.StaticLogError(errorMsg);
                        return FormatErrorAndLog(errorMsg);
                    }
                }
                catch (Autodesk.Revit.Exceptions.InvalidOperationException ex)
                {
                    var errorMsg = $"Revit InvalidOperationException when raising ExternalEvent: {ex.Message}. " +
                                  "This typically occurs when trying to execute Revit API code from an invalid thread context.";
                    DebugLogService.StaticLogError(errorMsg);
                    return FormatErrorAndLog(errorMsg);
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Unexpected error raising ExternalEvent: {ex.Message}";
                    DebugLogService.StaticLogError(errorMsg);
                    return FormatErrorAndLog(errorMsg);
                }

                // Wait for the external event to complete with a timeout
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask).ConfigureAwait(false);
                
                if (completedTask == timeoutTask)
                {
                    const string errorMsg = "Python execution timed out after 30 seconds";
                    DebugLogService.StaticLogError(errorMsg);
                    return FormatErrorAndLog(errorMsg);
                }

                var result = await tcs.Task.ConfigureAwait(false);
                DebugLogService.StaticLogInfo("ExecuteAsync: Python execution completed successfully");
                return result;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Fatal error in ExecuteAsync: {ex.Message}";
                DebugLogService.StaticLogError($"{errorMsg}\nStackTrace: {ex.StackTrace}");
                return FormatErrorAndLog(errorMsg);
            }
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
                if (uiapp != null)
                {
                    InjectRevitContext();
                }

                var (printOutput, result) = ExecuteWithCapturedStdout(code);
                var output = ComposeOutput(printOutput, result);

                return FormatSuccessAndLog(output);
            }
            catch (Exception ex)
            {
                return FormatErrorAndLog(ex.Message);
            }
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
            DebugLogService.StaticLogPythonOutput(StartMarker);
            DebugLogService.StaticLogPythonOutput($"Output: {output}");
            DebugLogService.StaticLogPythonOutput(EndMarker);

            var sb = new StringBuilder();
            sb.AppendLine(StartMarker);
            sb.AppendLine($"Output: {output}");
            sb.AppendLine(EndMarker);
            return sb.ToString();
        }

        private static string FormatErrorAndLog(string errorMessage)
        {
            DebugLogService.StaticLogError(ErrorStartMarker);
            DebugLogService.StaticLogError($"Python Error: {errorMessage}");
            DebugLogService.StaticLogError(ErrorEndMarker);

            var sb = new StringBuilder();
            sb.AppendLine(ErrorStartMarker);
            sb.AppendLine($"Python Error: {errorMessage}");
            sb.AppendLine(ErrorEndMarker);
            return sb.ToString();
        }

        /// <summary>
        /// Executes Python code with automatic path selection based on current thread context.
        /// Uses ExecuteAsync for UI threads, ExecuteSync for Revit API threads.
        /// </summary>
        /// <param name="code">The Python code to execute.</param>
        /// <returns>The execution result.</returns>
        public async Task<string> ExecuteSmartAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return string.Empty;

            try
            {
                // Try to detect if we're in a Revit API context by checking thread name and context
                var currentThread = System.Threading.Thread.CurrentThread;
                var isMainThread = currentThread.GetApartmentState() == System.Threading.ApartmentState.STA;
                
                DebugLogService.StaticLogInfo($"ExecuteSmartAsync: Thread info - Name: '{currentThread.Name}', " +
                                            $"IsThreadPoolThread: {currentThread.IsThreadPoolThread}, " +
                                            $"ApartmentState: {currentThread.GetApartmentState()}");

                // First, try to determine if we can execute synchronously
                // This is a heuristic - in a real implementation, you might have better context detection
                bool canUseSyncExecution = false;
                
                if (uiapp != null)
                {
                    try
                    {
                        // Try a simple Revit API call to see if we're in the right context
                        var _ = uiapp.Application.VersionName;
                        canUseSyncExecution = true;
                        DebugLogService.StaticLogInfo("ExecuteSmartAsync: Direct Revit API access successful, using sync execution");
                    }
                    catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                    {
                        // We're not in Revit API context
                        DebugLogService.StaticLogInfo("ExecuteSmartAsync: Not in Revit API context, using async execution");
                    }
                    catch (Exception ex)
                    {
                        DebugLogService.StaticLogError($"ExecuteSmartAsync: Error testing Revit context: {ex.Message}");
                    }
                }

                if (canUseSyncExecution)
                {
                    // We're in Revit API context, use sync execution
                    return ExecuteSync(code);
                }
                else
                {
                    // We need to marshal to Revit context, use async execution
                    return await ExecuteAsync(code);
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error in ExecuteSmartAsync: {ex.Message}";
                DebugLogService.StaticLogError($"{errorMsg}\nStackTrace: {ex.StackTrace}");
                return FormatErrorAndLog(errorMsg);
            }
        }
        private class ExecutePythonExternalEventHandler : IExternalEventHandler
        {
            private readonly PythonExecutionService service;
            private string? pendingCode;
            private TaskCompletionSource<string>? pendingTcs;

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

                    var (printOutput, result) = service.ExecuteWithCapturedStdout(pendingCode!);
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
