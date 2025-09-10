using System;
using System.Threading.Tasks;
using Autodesk.Revit.UI;

namespace Rca.Loader
{
    /// <summary>
    /// Service for handling pipe commands that interact with the runtime.
    /// </summary>
    public class RuntimeCommandHandler
    {
        private readonly RuntimeManager runtimeManager;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeCommandHandler"/> class.
        /// </summary>
        /// <param name="runtimeManager">The runtime manager.</param>
        public RuntimeCommandHandler(RuntimeManager runtimeManager)
        {
            this.runtimeManager = runtimeManager ?? throw new ArgumentNullException(nameof(runtimeManager));
        }
        
        /// <summary>
        /// Handles a pipe command asynchronously.
        /// </summary>
        /// <param name="cmd">The command to handle.</param>
        /// <returns>A response to the command.</returns>
        public async Task<PipeResponse> HandlePipeCommandAsync(PipeCommand cmd)
        {
            // This method doesn't actually need to be async, but keeping it async 
            // for potential future commands that may require async operations
            return await Task.FromResult(HandlePipeCommand(cmd));
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
                    return new PipeResponse { 
                        Status = runtimeManager.IsRuntimeLoaded ? "LOADED" : "EMPTY",
                        Message = runtimeManager.CurrentRuntimePath
                    };
                    
                default:
                    return new PipeResponse { 
                        Status = "ERROR", 
                        Message = "Unknown command" 
                    };
            }
        }
    }
}