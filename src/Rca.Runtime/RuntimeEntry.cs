using System;
using System.Diagnostics;
using Rca.Contracts.Infrastructure;
using Rca.Core.Services;
using Rca.Contracts;

namespace Rca.Runtime
{
    /// <summary>
    /// Main entry point for the runtime module that's loaded by the Rca.Loader.
    /// </summary>
    public class RuntimeEntry
    {
        private readonly ServiceContainer container;
        
        /// <summary>
        /// Initializes a new instance of the RuntimeEntry class.
        /// </summary>
        public RuntimeEntry()
        {
            // Initialize the container in the constructor
            container = ServiceContainer.Instance;
        }
        
        /// <summary>
        /// Initializes the runtime and sets up required services
        /// </summary>
        public void Initialize()
        {
            try
            {
                Debug.WriteLine("RCA Runtime initializing...");
                
                // Register core services
                RegisterServices();
                
                Debug.WriteLine("RCA Runtime initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing runtime: {ex}");
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
                    Debug.WriteLine("Registering StandaloneRevitContext");
                    container.Register<IRevitContext>(new StandaloneRevitContext());
                }
                
                // Register the debug log service if not already registered
                if (!container.IsRegistered<IDebugLogService>())
                {
                    Debug.WriteLine("Registering DebugLogService");
                    container.Register<IDebugLogService>(DebugLogService.Instance);
                }
                
                // Register the Python execution service if not already registered
                if (!container.IsRegistered<IPythonExecutionService>())
                {
                    Debug.WriteLine("Registering PythonExecutionService");
                    container.Register<IPythonExecutionService>(new PythonExecutionService());
                }
                
                // Other service registrations would go here
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error registering services: {ex}");
            }
        }

        /// <summary>
        /// Shuts down the runtime and performs cleanup
        /// </summary>
        public void Shutdown()
        {
            try
            {
                Debug.WriteLine("RCA Runtime shutting down...");
                
                // Perform any cleanup needed
                
                Debug.WriteLine("RCA Runtime shutdown complete");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error shutting down runtime: {ex}");
            }
        }
    }
}
