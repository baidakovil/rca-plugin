using System;
using System.Runtime.Loader;

namespace Rca.Loader
{
    /// <summary>
    /// A collectible assembly load context for hot reloading the runtime assembly.
    /// </summary>
    internal class HotReloadAssemblyLoadContext : AssemblyLoadContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HotReloadAssemblyLoadContext"/> class.
        /// </summary>
        /// <param name="name">The name of the assembly load context.</param>
        public HotReloadAssemblyLoadContext(string name) : base(name, isCollectible: true)
        {
        }

        /// <summary>
        /// Gets a weak reference to this context for monitoring collection.
        /// </summary>
        public WeakReference GetWeakReference()
        {
            return new WeakReference(this);
        }
    }
}