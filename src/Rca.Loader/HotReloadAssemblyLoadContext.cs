using System;
using System.Reflection;
using System.Runtime.Loader;

namespace Rca.Loader
{
    /// <summary>
    /// Collectible assembly load context for hot reloading.
    /// </summary>
    public class HotReloadAssemblyLoadContext : AssemblyLoadContext
    {
        /// <summary>
        /// Initializes a new instance of the HotReloadAssemblyLoadContext class.
        /// </summary>
        /// <param name="name">The name of the context</param>
        public HotReloadAssemblyLoadContext(string name) : base(name, isCollectible: true)
        {
        }

        /// <summary>
        /// Load an assembly from the specified path.
        /// </summary>
        /// <param name="assemblyPath">Path to the assembly</param>
        /// <returns>The loaded assembly</returns>
        public Assembly LoadAssemblyFromPath(string assemblyPath)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        /// <summary>
        /// Resolves assemblies that are not found in the default locations.
        /// </summary>
        protected override Assembly Load(AssemblyName assemblyName)
        {
            // Let the default context handle system assemblies
            return null;
        }
    }
}