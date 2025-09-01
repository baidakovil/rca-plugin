using System;
using System.Runtime.Loader;

namespace Rca.Loader
{
    /// <summary>
    /// Collectible assembly load context for hot reload functionality.
    /// </summary>
    public class HotReloadAssemblyLoadContext : AssemblyLoadContext
    {
        /// <summary>
        /// Initializes a new instance of the HotReloadAssemblyLoadContext class.
        /// </summary>
        /// <param name="name">The name of the load context</param>
        public HotReloadAssemblyLoadContext(string name) : base(name, isCollectible: true)
        {
        }

        /// <summary>
        /// Loads an assembly given its path.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly</param>
        /// <returns>The loaded assembly</returns>
        public System.Reflection.Assembly LoadAssembly(string assemblyPath)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }
    }
}