using System;
using System.Collections.Generic;

namespace Rca.Loader.Contracts
{
    /// <summary>
    /// Shared service registry that bridges Loader and Runtime AssemblyLoadContexts.
    /// Lives in Loader's non-collectible context and can be accessed from Runtime.
    /// </summary>
    public static class SharedServiceRegistry
    {
        private static readonly Dictionary<Type, object> Services = new();
        private static readonly object Lock = new();

        /// <summary>
        /// Registers a service instance.
        /// Thread-safe operation.
        /// </summary>
        /// <typeparam name="TInterface">The interface type</typeparam>
        /// <param name="implementation">The implementation instance</param>
        public static void Register<TInterface>(TInterface implementation) where TInterface : class
        {
            if (implementation == null)
                throw new ArgumentNullException(nameof(implementation));

            lock (Lock)
            {
                Services[typeof(TInterface)] = implementation;
            }
        }

        /// <summary>
        /// Resolves a service instance.
        /// Thread-safe operation.
        /// </summary>
        /// <typeparam name="TInterface">The interface type</typeparam>
        /// <returns>The service instance, or null if not registered</returns>
        public static TInterface? Resolve<TInterface>() where TInterface : class
        {
            lock (Lock)
            {
                if (Services.TryGetValue(typeof(TInterface), out var service))
                {
                    return service as TInterface;
                }
                return null;
            }
        }

        /// <summary>
        /// Checks if a service is registered.
        /// Thread-safe operation.
        /// </summary>
        /// <typeparam name="TInterface">The interface type</typeparam>
        /// <returns>True if the service is registered, otherwise false</returns>
        public static bool IsRegistered<TInterface>() where TInterface : class
        {
            lock (Lock)
            {
                return Services.ContainsKey(typeof(TInterface));
            }
        }

        /// <summary>
        /// Clears all registered services.
        /// Should be called during Runtime unload.
        /// </summary>
        public static void Clear()
        {
            lock (Lock)
            {
                Services.Clear();
            }
        }
    }
}
