using Rca.Contracts;
using Rca.Contracts.Infrastructure;
using System;
using System.Diagnostics;

namespace Rca.UI.Services
{
    /// <summary>
    /// Service for resolving dependencies with graceful fallbacks to default implementations.
    /// </summary>
    public class ServiceResolver
    {
        private readonly ServiceContainer container;

        /// <summary>
        /// Initializes a new instance of the ServiceResolver class.
        /// </summary>
        /// <param name="container">The service container to resolve from.</param>
        public ServiceResolver(ServiceContainer container)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
        }

        /// <summary>
        /// Resolves a service from the container or returns a default implementation.
        /// </summary>
        /// <typeparam name="T">The service interface type.</typeparam>
        /// <returns>The resolved service or a default implementation.</returns>
        public T ResolveOrDefault<T>() where T : class
        {
            try
            {
                if (container.IsRegistered<T>())
                {
                    return container.Resolve<T>();
                }
                
                LogServiceNotRegistered<T>();
                return CreateDefaultService<T>();
            }
            catch (InvalidOperationException ex)
            {
                LogServiceResolutionError<T>(ex);
                return CreateDefaultService<T>();
            }
        }

        /// <summary>
        /// Creates a default implementation for the specified service type.
        /// </summary>
        /// <typeparam name="T">The service interface type.</typeparam>
        /// <returns>A default implementation or null if no default is available.</returns>
        private static T CreateDefaultService<T>() where T : class
        {
            return typeof(T) switch
            {
                var t when t == typeof(IPythonExecutionService) => DefaultServices.CreatePythonExecutionService() as T,
                var t when t == typeof(IDebugLogService) => DefaultServices.CreateDebugLogService() as T,
                var t when t == typeof(IRevitContext) => DefaultServices.CreateRevitContext() as T,
                _ => null
            };
        }

        /// <summary>
        /// Logs that a service is not registered.
        /// </summary>
        /// <typeparam name="T">The service type.</typeparam>
        private static void LogServiceNotRegistered<T>()
        {
            Debug.WriteLine($"Service {typeof(T).Name} not registered. Using default implementation.");
        }

        /// <summary>
        /// Logs a service resolution error.
        /// </summary>
        /// <typeparam name="T">The service type.</typeparam>
        /// <param name="ex">The exception that occurred.</param>
        private static void LogServiceResolutionError<T>(Exception ex)
        {
            Debug.WriteLine($"Error resolving service {typeof(T).Name}: {ex.Message}");
        }
    }
}