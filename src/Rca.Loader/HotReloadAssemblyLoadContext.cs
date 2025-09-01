using System;
using System.Reflection;
using System.Runtime.Loader;

namespace Rca.Loader
{
    /// <summary>
    /// Collectible AssemblyLoadContext for hot-reloadable runtime assemblies.
    /// </summary>
    public class HotReloadAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly string assemblyPath;

        /// <summary>
        /// Initializes a new instance of the HotReloadAssemblyLoadContext class.
        /// </summary>
        /// <param name="assemblyPath">Path to the assembly to load</param>
        public HotReloadAssemblyLoadContext(string assemblyPath) : base(name: $"HotReload_{Guid.NewGuid():N}", isCollectible: true)
        {
            this.assemblyPath = assemblyPath ?? throw new ArgumentNullException(nameof(assemblyPath));
        }

        /// <summary>
        /// Loads the assembly into this context.
        /// </summary>
        /// <returns>The loaded assembly</returns>
        public Assembly LoadAssembly()
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        /// <summary>
        /// Loads assemblies for this context.
        /// </summary>
        protected override Assembly Load(AssemblyName assemblyName)
        {
            // Let the default context handle most dependencies
            return null;
        }
    }
}