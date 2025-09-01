using Autodesk.Revit.UI;
using Rca.Loader.Contracts;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Rca.Loader
{
    /// <summary>
    /// Manages the runtime assembly loading, unloading, and reloading using AssemblyLoadContext.
    /// </summary>
    public class RuntimeHost
    {
        private CollectibleAssemblyLoadContext currentContext;
        private IRcaRuntime currentRuntime;
        private readonly string runtimeAssemblyName = "Rca.Runtime.dll";
        private readonly string tempDirectory;

        /// <summary>
        /// Initializes a new instance of the RuntimeHost.
        /// </summary>
        public RuntimeHost()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "RcaLoader", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDirectory);
        }

        /// <summary>
        /// Gets the current runtime instance.
        /// </summary>
        public IRcaRuntime CurrentRuntime => currentRuntime;

        /// <summary>
        /// Loads the runtime assembly into a new collectible context.
        /// </summary>
        public bool LoadRuntime()
        {
            try
            {
                Console.WriteLine("[HotReload] Loading runtime...");
                
                // Find the runtime assembly
                var loaderAssemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var runtimeAssemblyPath = Path.Combine(loaderAssemblyDir, runtimeAssemblyName);
                
                if (!File.Exists(runtimeAssemblyPath))
                {
                    Console.WriteLine($"[HotReload] Runtime assembly not found at: {runtimeAssemblyPath}");
                    return false;
                }

                // Copy runtime to temp directory for shadow copy
                var tempRuntimePath = Path.Combine(tempDirectory, runtimeAssemblyName);
                File.Copy(runtimeAssemblyPath, tempRuntimePath, true);

                // Create new collectible context
                currentContext = new CollectibleAssemblyLoadContext();
                
                // Load the runtime assembly
                var runtimeAssembly = currentContext.LoadFromAssemblyPath(tempRuntimePath);
                
                // Find and instantiate the runtime implementation
                var runtimeType = runtimeAssembly.GetType("Rca.Runtime.RcaRuntimeApp");
                if (runtimeType == null)
                {
                    Console.WriteLine("[HotReload] Runtime implementation type not found");
                    return false;
                }

                currentRuntime = (IRcaRuntime)Activator.CreateInstance(runtimeType);
                
                Console.WriteLine($"[HotReload] Runtime loaded successfully, version: {currentRuntime.Version}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HotReload] Failed to load runtime: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Starts up the currently loaded runtime.
        /// </summary>
        public bool StartupRuntime(UIControlledApplication application)
        {
            try
            {
                if (currentRuntime == null)
                {
                    Console.WriteLine("[HotReload] No runtime loaded to start");
                    return false;
                }

                Console.WriteLine("[HotReload] Starting runtime...");
                return currentRuntime.Startup(application);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HotReload] Failed to startup runtime: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Unloads the current runtime and cleans up resources.
        /// </summary>
        public void UnloadRuntime()
        {
            try
            {
                Console.WriteLine("[HotReload] Unloading runtime...");
                
                // Shutdown runtime if available
                currentRuntime?.Shutdown();
                currentRuntime = null;

                // Unload assembly context
                if (currentContext != null)
                {
                    currentContext.Unload();
                    currentContext = null;
                }

                // Force garbage collection to release assemblies
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                Console.WriteLine("[HotReload] Runtime unloaded");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HotReload] Error during runtime unload: {ex.Message}");
            }
        }

        /// <summary>
        /// Reloads the runtime by unloading current and loading fresh copy.
        /// </summary>
        public bool ReloadRuntime(UIControlledApplication application)
        {
            try
            {
                Console.WriteLine("[HotReload] Reloading runtime...");
                
                UnloadRuntime();
                
                if (!LoadRuntime())
                {
                    Console.WriteLine("[HotReload] Failed to load new runtime during reload");
                    return false;
                }

                if (!StartupRuntime(application))
                {
                    Console.WriteLine("[HotReload] Failed to startup new runtime during reload");
                    return false;
                }

                Console.WriteLine("[HotReload] Runtime reload completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HotReload] Runtime reload failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Collectible AssemblyLoadContext for hot-reload scenarios.
    /// </summary>
    internal class CollectibleAssemblyLoadContext : AssemblyLoadContext
    {
        public CollectibleAssemblyLoadContext() : base(isCollectible: true)
        {
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            // Don't load RevitAPI assemblies - let them be resolved from the default context
            if (assemblyName.Name == "RevitAPI" || assemblyName.Name == "RevitAPIUI")
            {
                return null;
            }
            
            return null; // Let default resolution handle other assemblies
        }
    }
}