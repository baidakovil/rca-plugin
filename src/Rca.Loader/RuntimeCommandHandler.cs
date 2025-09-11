using System;
using System.Text.Json;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Rca.Loader.Testing;
using System.Diagnostics;

namespace Rca.Loader
{
    /// <summary>
    /// Service for handling pipe commands that interact with the runtime.
    /// </summary>
    public class RuntimeCommandHandler
    {
        private readonly RuntimeManager runtimeManager;
        private readonly UIApplication uiapp;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeCommandHandler"/> class.
        /// </summary>
        /// <param name="runtimeManager">The runtime manager.</param>
        /// <param name="uiapp">The Revit UI application.</param>
        public RuntimeCommandHandler(RuntimeManager runtimeManager, UIApplication uiapp)
        {
            this.runtimeManager = runtimeManager ?? throw new ArgumentNullException(nameof(runtimeManager));
            this.uiapp = uiapp ?? throw new ArgumentNullException(nameof(uiapp));
            
            Debug.WriteLine("DEBUG: RuntimeCommandHandler initialized");
        }
        
        /// <summary>
        /// Handles a pipe command asynchronously.
        /// </summary>
        /// <param name="cmd">The command to handle.</param>
        /// <returns>A response to the command.</returns>
        public async Task<PipeResponse> HandlePipeCommandAsync(PipeCommand cmd)
        {
            try
            {
                Debug.WriteLine($"DEBUG: Handling command: {cmd.Command}, Payload: {(cmd.Payload?.Length > 100 ? cmd.Payload?.Substring(0, 100) + "..." : cmd.Payload)}");
                
                switch (cmd.Command)
                {
                    case "RUN_TESTS":
                        return await HandleRunTestsCommandAsync(cmd);
                    case "TEST_INIT":
                        return await Task.FromResult(new PipeResponse { Status = "OK", Message = "Test execution ready" });
                    default:
                        return await Task.FromResult(HandlePipeCommand(cmd));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DEBUG: Error handling command: {ex}");
                return new PipeResponse 
                { 
                    Status = "ERROR", 
                    Message = $"Error handling command: {ex.Message}" 
                };
            }
        }
        
        /// <summary>
        /// Handles a pipe command synchronously.
        /// </summary>
        /// <param name="cmd">The command to handle.</param>
        /// <returns>A response to the command.</returns>
        private PipeResponse HandlePipeCommand(PipeCommand cmd)
        {
            switch (cmd.Command)
            {
                case "RELOAD":
                    var path = cmd.Payload;
                    var result = runtimeManager.ReloadRuntime(path, out var errorMessage);
                    return new PipeResponse { 
                        Status = result ? "OK" : "ERROR", 
                        Message = errorMessage ?? string.Empty 
                    };
                    
                case "STATUS":
                    var isRuntimeLoaded = runtimeManager.IsRuntimeLoaded;
                    Debug.WriteLine($"DEBUG: STATUS command - IsRuntimeLoaded: {isRuntimeLoaded}");
                    
                    return new PipeResponse { 
                        Status = isRuntimeLoaded ? "LOADED" : "EMPTY",
                        Message = runtimeManager.CurrentRuntimePath
                    };
                    
                default:
                    return new PipeResponse { 
                        Status = "ERROR", 
                        Message = "Unknown command" 
                    };
            }
        }
        
        private async Task<PipeResponse> HandleRunTestsCommandAsync(PipeCommand cmd)
        {
            if (string.IsNullOrEmpty(cmd.Payload))
            {
                return new PipeResponse { Status = "ERROR", Message = "Empty test payload" };
            }
            
            try
            {
                Debug.WriteLine("DEBUG: Deserializing test execution payload");
                
                // Deserialize the test execution payload
                var payload = JsonSerializer.Deserialize<RevitTestExecutor.TestExecutionPayload>(cmd.Payload);
                if (payload == null)
                {
                    Debug.WriteLine("DEBUG: Invalid test payload format");
                    return new PipeResponse { Status = "ERROR", Message = "Invalid test payload format" };
                }
                
                Debug.WriteLine($"DEBUG: Creating test executor, AssemblyPath: {payload.AssemblyPath}, Tests count: {payload.Tests.Count}");
                
                // Create a test executor
                var testExecutor = new RevitTestExecutor(uiapp);
                
                // Execute the tests - this could be CPU intensive for large test suites,
                // so run it on a background thread to avoid blocking the UI
                var results = await Task.Run(() => testExecutor.ExecuteTests(payload.AssemblyPath, payload.Tests));
                
                // Serialize the results
                Debug.WriteLine($"DEBUG: Tests executed, results count: {results.Count}");
                var resultsJson = JsonSerializer.Serialize(results);
                
                return new PipeResponse { Status = "OK", Message = resultsJson };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DEBUG: Test execution error: {ex}");
                return new PipeResponse { Status = "ERROR", Message = $"Test execution error: {ex.Message}" };
            }
        }
    }
}