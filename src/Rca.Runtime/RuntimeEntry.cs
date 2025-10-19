using System;
using Microsoft.Extensions.Logging;
using Rca.Core.Services;
using Rca.Contracts;
using Rca.Runtime.Logging;
using Rca.Loader.Contracts;
using Rca.Runtime.UI;

namespace Rca.Runtime
{
    /// <summary>
    /// Main entry point for the runtime module loaded by the Loader. Uses named pipe logger provider.
    /// </summary>
    public class RuntimeEntry
    {
        private readonly ILogger _log;
        private static readonly string SessionId = Guid.NewGuid().ToString("N");
        private static NamedPipeLoggerProvider? _provider;

        /// <summary>
        /// Initializes a new instance of the RuntimeEntry class.
        /// </summary>
        public RuntimeEntry()
        {
            _provider ??= new NamedPipeLoggerProvider("RCA_LOG_PIPE", SessionId);
            _log = _provider.CreateLogger(nameof(RuntimeEntry));
        }

        /// <summary>
        /// Gets the shared logger provider instance for use across Runtime components.
        /// This ensures all Runtime logging uses the same pipe connection.
        /// </summary>
        /// <returns>The shared NamedPipeLoggerProvider instance.</returns>
        public static NamedPipeLoggerProvider GetLoggerProvider()
        {
            if (_provider == null)
            {
                throw new InvalidOperationException("LoggerProvider not initialized. RuntimeEntry must be instantiated first.");
            }
            return _provider;
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
        /// Registers required services in SharedServiceRegistry for cross-context access.
        /// </summary>
        private void RegisterServices()
        {
            try
            {
                // Register Python execution service
                var pythonService = new PythonExecutionService();
                SharedServiceRegistry.Register<IPythonExecutionService>(pythonService);
                _log.LogDebug("Registered PythonExecutionService");
                
                // Register Revit context
                var revitContext = new StandaloneRevitContext();
                SharedServiceRegistry.Register<IRevitContext>(revitContext);
                _log.LogDebug("Registered StandaloneRevitContext");

                // Register panel factory for UI creation
                var factory = new RuntimePanelFactory();
                SharedServiceRegistry.Register<IRuntimePanelFactory>(factory);
                _log.LogDebug("Registered RuntimePanelFactory");
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
                
                // Clear shared registry to release references
                SharedServiceRegistry.Clear();
                _log.LogDebug("Cleared SharedServiceRegistry");
                
                _log.LogInformation("Runtime shutdown complete");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error during runtime shutdown");
            }
        }
    }
}
