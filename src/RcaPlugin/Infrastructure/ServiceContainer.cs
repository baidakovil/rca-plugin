using Rca.Contracts;
using System;
using System.Collections.Generic;

namespace RcaPlugin.Infrastructure
{
    /// <summary>
    /// Simple service container for dependency injection.
    /// </summary>
    public class ServiceContainer
    {
        private readonly Dictionary<Type, object> services = new();
        private static ServiceContainer instance;

        /// <summary>
        /// Gets the singleton instance of the service container.
        /// </summary>
        public static ServiceContainer Instance => instance ??= new ServiceContainer();

        /// <summary>
        /// Registers a service instance.
        /// </summary>
        /// <typeparam name="TInterface">The interface type</typeparam>
        /// <param name="implementation">The implementation instance</param>
        public void Register<TInterface>(TInterface implementation)
        {
            services[typeof(TInterface)] = implementation;
        }

        /// <summary>
        /// Resolves a service instance.
        /// </summary>
        /// <typeparam name="TInterface">The interface type</typeparam>
        /// <returns>The service instance</returns>
        public TInterface Resolve<TInterface>()
        {
            if (services.TryGetValue(typeof(TInterface), out var service))
            {
                return (TInterface)service;
            }
            throw new InvalidOperationException($"Service of type {typeof(TInterface).Name} is not registered.");
        }

        /// <summary>
        /// Checks if a service is registered.
        /// </summary>
        /// <typeparam name="TInterface">The interface type</typeparam>
        /// <returns>True if the service is registered, otherwise false</returns>
        public bool IsRegistered<TInterface>()
        {
            return services.ContainsKey(typeof(TInterface));
        }
    }
}