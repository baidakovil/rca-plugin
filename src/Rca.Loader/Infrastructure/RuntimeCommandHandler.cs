using System;
using System.Text.Json;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Rca.Loader.Testing;
using Rca.Loader.Contracts;
using Rca.Loader.Services;
using Rca.Loader.Infrastructure;

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
            try
            {
                // Validate command first
                if (!validationService.ValidateCommand(cmd, out var validationError))
                {
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
            return cmd.Command.ToUpperInvariant() switch
            {
                PipeCommands.Reload => HandleReloadCommand(cmd),
                PipeCommands.Status => HandleStatusCommand(),
                _ => PipeResponseFactory.UnknownCommand(cmd.Command)
            };
        }

        private PipeResponse HandleReloadCommand(PipeCommand cmd)
        {
            var result = runtimeManager.ReloadRuntime(cmd.Payload, out var errorMessage);
            return result 
                ? PipeResponseFactory.Success(errorMessage ?? string.Empty)
                : PipeResponseFactory.Error(errorMessage ?? "Unknown reload error");
        }

        private PipeResponse HandleStatusCommand()
        {
            var isRuntimeLoaded = runtimeManager.IsRuntimeLoaded;
            return isRuntimeLoaded 
                ? PipeResponseFactory.Loaded(runtimeManager.CurrentRuntimePath)
                : PipeResponseFactory.Empty();
        }

        private async Task<PipeResponse> HandleTestInitCommandAsync()
        {
            return await Task.FromResult(PipeResponseFactory.Success("Test execution ready"));
        }
        
        private async Task<PipeResponse> HandleRunTestsCommandAsync(PipeCommand cmd)
        {
            if (string.IsNullOrEmpty(cmd.Payload))
            {
                return PipeResponseFactory.InvalidPayload("Empty test payload");
            }
            
            try
            {
                // Deserialize the test execution payload
                var payload = JsonSerializer.Deserialize<RevitTestExecutor.TestExecutionPayload>(cmd.Payload);
                if (payload == null)
                {
                    return PipeResponseFactory.InvalidPayload("Invalid test payload format");
                }
                
                // Create a test executor
                var testExecutor = new RevitTestExecutor(uiapp);
                
                // Execute the tests - this could be CPU intensive for large test suites,
                // so run it on a background thread to avoid blocking the UI
                var results = await Task.Run(() => testExecutor.ExecuteTests(payload.AssemblyPath, payload.Tests));
                
                // Serialize the results
                var resultsJson = JsonSerializer.Serialize(results);
                
                return PipeResponseFactory.Success(resultsJson);
            }
            catch (JsonException ex)
            {
                return PipeResponseFactory.Error($"JSON serialization error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return PipeResponseFactory.Error($"Test execution error: {ex.Message}");
            }
        }
    }
}
