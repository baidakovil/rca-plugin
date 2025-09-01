using System.Runtime.Loader;

namespace Rca.Loader.Services
{
    /// <summary>
    /// Collectible AssemblyLoadContext for hot reloading runtime assemblies.
    /// This enables true unloading of assemblies in .NET 8.
    /// </summary>
    public class HotReloadAssemblyLoadContext : AssemblyLoadContext
    {
        /// <summary>
        /// Initializes a new instance of the HotReloadAssemblyLoadContext.
        /// </summary>
        public HotReloadAssemblyLoadContext() : base(isCollectible: true)
        {
        }
    }
}