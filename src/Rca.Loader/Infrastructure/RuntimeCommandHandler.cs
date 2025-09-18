using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using Autodesk.Revit.UI;
using Rca.Loader.Testing;
using Rca.Loader.Contracts;
using Rca.Loader.Services;
using Rca.Loader.Infrastructure;
using Rca.Loader.AssemblyManagement;

namespace Rca.Loader.Infrastructure
{
    /// <summary>
    /// Service for handling pipe commands that interact with the runtime.
    /// </summary>
    public class RuntimeCommandHandler
    {
        private readonly IRuntimeManager runtimeManager;
        private readonly UIApplication uiapp;
        private readonly CommandValidationService validationService;
        private readonly AssemblyStatusManager? assemblyStatusManager;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeCommandHandler"/> class.
        /// </summary>
        /// <param name="runtimeManager">The runtime manager.</param>
        /// <param name="uiapp">The Revit UI application.</param>
        public RuntimeCommandHandler(IRuntimeManager runtimeManager, UIApplication uiapp)
        {
            this.runtimeManager = runtimeManager ?? throw new ArgumentNullException(nameof(runtimeManager));
            this.uiapp = uiapp ?? throw new ArgumentNullException(nameof(uiapp));
            this.validationService = new CommandValidationService();
            this.assemblyStatusManager = LoaderApp.Instance?.AssemblyStatusManager;
            
            if (this.assemblyStatusManager == null)
            {
                Debug.WriteLine("Warning: AssemblyStatusManager not available in RuntimeCommandHandler");
            }
        }
        
        /// <summary>
        /// Handles a pipe command asynchronously.
        /// </summary>
        /// <param name="cmd">The command to handle.</param>
        /// <returns>A response to the command.</returns>
        public async Task<PipeResponse> HandlePipeCommandAsync(PipeCommand cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException(nameof(cmd));
                
            Debug.WriteLine($"Received pipe command: {cmd.Command}");
            
            try
            {
                // Validate command first
                if (!validationService.ValidateCommand(cmd, out var validationError))
                {
                    Debug.WriteLine($"Command validation failed: {validationError}");
                    return PipeResponseFactory.InvalidPayload(validationError);
                }

                return cmd.Command.ToUpperInvariant() switch
                {
                    PipeCommands.RunTests => await HandleRunTestsCommandAsync(cmd),
                    PipeCommands.TestInit => await HandleTestInitCommandAsync(),
                    _ => await Task.FromResult(HandleSyncCommand(cmd))
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling command: {ex.Message}\n{ex.StackTrace}");
                return PipeResponseFactory.Error($"Error handling command: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Handles synchronous pipe commands.
        /// </summary>
        /// <param name="cmd">The command to handle.</param>
        /// <returns>A response to the command.</returns>
        private PipeResponse HandleSyncCommand(PipeCommand cmd)
        {
            Debug.WriteLine($"Handling synchronous command: {cmd.Command}");
            
            return cmd.Command.ToUpperInvariant() switch
            {
                PipeCommands.Reload => HandleReloadCommand(cmd),
                PipeCommands.ReloadRuntime => HandleReloadRuntimeCommand(cmd),
                PipeCommands.Status => HandleStatusCommand(),
                _ => PipeResponseFactory.UnknownCommand(cmd.Command)
            };
        }

        private PipeResponse HandleReloadCommand(PipeCommand cmd)
        {
            Debug.WriteLine($"Handling RELOAD command with payload: {cmd.Payload}");
            
            try
            {
                var result = runtimeManager.ReloadRuntime(cmd.Payload, out var errorMessage);
                
                if (result && !string.IsNullOrEmpty(cmd.Payload))
                {
                    // Update status manager about the change
                    assemblyStatusManager?.ProcessMsBuildSignal(cmd.Payload);
                }
                
                return result 
                    ? PipeResponseFactory.Success(errorMessage ?? string.Empty)
                    : PipeResponseFactory.Error(errorMessage ?? "Unknown reload error");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in HandleReloadCommand: {ex.Message}");
                return PipeResponseFactory.Error($"Error reloading: {ex.Message}");
            }
        }
        
        private PipeResponse HandleReloadRuntimeCommand(PipeCommand cmd)
        {
            Debug.WriteLine($"Handling RELOAD_RUNTIME command with payload: {cmd.Payload}");
            
            try
            {
                // Ensure payload is not null
                string tempDllPath = cmd.Payload ?? string.Empty;
                
                if (string.IsNullOrEmpty(tempDllPath))
                {
                    return PipeResponseFactory.Error("Invalid path: payload is null or empty");
                }
                
                // Process MSBuild signal to check what has changed
                if (assemblyStatusManager != null)
                {
                    Debug.WriteLine("Processing MSBuild signal to check what has changed");
                    assemblyStatusManager.ProcessMsBuildSignal(tempDllPath);
                    
                    // Check if only loader is outdated or both are outdated
                    bool loaderOutdated = assemblyStatusManager.IsLoaderOutdated();
                    bool runtimeOutdated = assemblyStatusManager.IsRuntimeOutdated();
                    
                    if (loaderOutdated && !runtimeOutdated)
                    {
                        Debug.WriteLine("Only loader is outdated, restart required but not runtime reload");
                        return PipeResponseFactory.Success("LOADER_RESTART_REQUIRED");
                    }
                    
                    if (!loaderOutdated && !runtimeOutdated)
                    {
                        Debug.WriteLine("Both loader and runtime are current, no action needed");
                        return PipeResponseFactory.Success("NO_ACTION_NEEDED");
                    }
                }
                
                // Reload runtime if needed
                Debug.WriteLine("Attempting to reload runtime...");
                var result = runtimeManager.ReloadRuntime(tempDllPath, out var errorMessage);
                
                if (result)
                {
                    // Update runtime hash if reload was successful
                    if (assemblyStatusManager != null)
                    {
                        Debug.WriteLine("Updating runtime hash after successful reload");
                        assemblyStatusManager.UpdateHashesAfterReload(runtimeManager.CurrentRuntimePath);
                    }
                    return PipeResponseFactory.Success("ReloadRuntime completed successfully");
                }
                else
                {
                    Debug.WriteLine($"Runtime reload failed: {errorMessage}");
                    return PipeResponseFactory.Error(errorMessage ?? "Unknown reload error");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in HandleReloadRuntimeCommand: {ex.Message}\n{ex.StackTrace}");
                return PipeResponseFactory.Error($"Error in ReloadRuntime: {ex.Message}");
            }
        }

        private PipeResponse HandleStatusCommand()
        {
            Debug.WriteLine("Handling STATUS command");
            
            try
            {
                var isRuntimeLoaded = runtimeManager.IsRuntimeLoaded;
                var path = isRuntimeLoaded ? runtimeManager.CurrentRuntimePath : string.Empty;
                
                Debug.WriteLine($"Runtime loaded: {isRuntimeLoaded}, Path: {path}");
                
                return isRuntimeLoaded 
                    ? PipeResponseFactory.Loaded(path)
                    : PipeResponseFactory.Empty();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in HandleStatusCommand: {ex.Message}");
                return PipeResponseFactory.Error($"Error getting status: {ex.Message}");
            }
        }

        private async Task<PipeResponse> HandleTestInitCommandAsync()
        {
            Debug.WriteLine("Handling TEST_INIT command");
            return await Task.FromResult(PipeResponseFactory.Success("Test execution ready"));
        }
        
        private async Task<PipeResponse> HandleRunTestsCommandAsync(PipeCommand cmd)
        {
            Debug.WriteLine("Handling RUN_TESTS command");
            
            if (string.IsNullOrEmpty(cmd.Payload))
            {
                Debug.WriteLine("Error: Empty test payload");
                return PipeResponseFactory.InvalidPayload("Empty test payload");
            }
            
            try
            {
                // Deserialize the test execution payload
                var payload = JsonSerializer.Deserialize<RevitTestExecutor.TestExecutionPayload>(cmd.Payload);
                if (payload == null)
                {
                    Debug.WriteLine("Error: Invalid test payload format");
                    return PipeResponseFactory.InvalidPayload("Invalid test payload format");
                }
                
                Debug.WriteLine($"Executing {payload.Tests.Count} tests from assembly: {payload.AssemblyPath}");
                
                // Create a test executor
                var testExecutor = new RevitTestExecutor(uiapp);
                
                // Execute the tests - this could be CPU intensive for large test suites,
                // so run it on a background thread to avoid blocking the UI
                var results = await Task.Run(() => testExecutor.ExecuteTests(payload.AssemblyPath, payload.Tests));
                
                // Serialize the results
                var resultsJson = JsonSerializer.Serialize(results);
                
                Debug.WriteLine($"Test execution completed with {results.Count} results");
                return PipeResponseFactory.Success(resultsJson);
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"JSON serialization error: {ex.Message}");
                return PipeResponseFactory.Error($"JSON serialization error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Test execution error: {ex.Message}\n{ex.StackTrace}");
                return PipeResponseFactory.Error($"Test execution error: {ex.Message}");
            }
        }
    }
}
