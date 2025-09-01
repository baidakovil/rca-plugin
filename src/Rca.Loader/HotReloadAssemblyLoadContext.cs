using System;
using System.Runtime.Loader;

namespace Rca.Loader
{
    /// <summary>
    /// Collectible AssemblyLoadContext for hot-reloadable runtime assemblies.
    /// </summary>
    internal class HotReloadAssemblyLoadContext : AssemblyLoadContext
    {
        /// <summary>
        /// Initializes a new instance of the HotReloadAssemblyLoadContext class.
        /// </summary>
        /// <param name="name">Name of the load context</param>
        public HotReloadAssemblyLoadContext(string name) : base(name, isCollectible: true)
        {
        }

        /// <summary>
        /// Gets a weak reference to this context for tracking collection.
        /// </summary>
        public WeakReference WeakReference => new WeakReference(this);
    }
}