using System;
using Microsoft.Extensions.Logging;
using Rca.Contracts.Infrastructure;
using Rca.Core.Services;
using Rca.Contracts;
using Rca.Runtime.Logging;

namespace Rca.Runtime
{
    /// <summary>
    /// Main entry point for the runtime module loaded by the Loader. Uses named pipe logger provider.
    /// </summary>
    public class RuntimeEntry
    {
        private readonly ServiceContainer container;
        private readonly ILogger _log;
        private static readonly string SessionId = Guid.NewGuid().ToString("N");
        private static NamedPipeLoggerProvider? _provider; // keep reference to avoid premature dispose

        /// <summary>
        /// Initializes a new instance of the RuntimeEntry class.
        /// </summary>
        public RuntimeEntry()
        {
            // Initialize the container in the constructor
            container = ServiceContainer.Instance;
            _provider ??= new NamedPipeLoggerProvider("RCA_LOG_PIPE", SessionId);
            _log = _provider.CreateLogger(nameof(RuntimeEntry));
        }

        /// <summary>
        /// Initializes the runtime and sets up required services
        /// </summary>
        public void Initialize()
        {
            try
            {
                _log.LogInformation("Runtime initializing (session={Session})", SessionId);
                
                // Register core services
                RegisterServices();
                
                _log.LogInformation("Runtime initialized successfully");
            }
            catch (Exception ex)
            {
                _log.LogCritical(ex, "Runtime initialization failed");
            }
        }

        /// <summary>
        /// Registers required services in the container
        /// </summary>
        private void RegisterServices()
        {
            try
            {
                // Register a standalone RevitContext if not already registered
                if (!container.IsRegistered<IRevitContext>())
                {
                    container.Register<IRevitContext>(new StandaloneRevitContext());
                    _log.LogDebug("Registered StandaloneRevitContext");
                }
                
                // Register the Python execution service if not already registered
                if (!container.IsRegistered<IPythonExecutionService>())
                {
                    container.Register<IPythonExecutionService>(new PythonExecutionService());
                    _log.LogDebug("Registered PythonExecutionService");
                }
                
                // Other service registrations would go here
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error registering runtime services");
            }
        }

        /// <summary>
        /// Shuts down the runtime and performs cleanup
        /// </summary>
        public void Shutdown()
        {
            try
            {
                _log.LogInformation("Runtime shutting down");
                
                // Perform any cleanup needed
                
                _log.LogInformation("Runtime shutdown complete");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error during runtime shutdown");
            }
        }
    }
}
