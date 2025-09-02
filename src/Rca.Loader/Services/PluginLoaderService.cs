using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Autodesk.Revit.UI;
using Rca.Contracts.Infrastructure;

namespace Rca.Loader.Services
{
    /// <summary>
    /// Plugin loader service using AssemblyLoadContext for isolated loading.
    /// </summary>
    public class PluginLoaderService : IPluginLoader
    {
        private PluginAssemblyLoadContext currentContext;
        private IExternalApplication currentPlugin;
        
        /// <summary>
        /// Event raised when plugin loading fails.
        /// </summary>
        public event EventHandler<string> LoadingFailed;

        /// <summary>
        /// Gets whether a plugin is currently loaded.
        /// </summary>
        public bool IsPluginLoaded => currentPlugin != null;

        /// <summary>
        /// Loads the plugin from the specified assembly path.
        /// </summary>
        /// <param name="assemblyPath">Path to the plugin assembly.</param>
        /// <returns>True if loaded successfully, false otherwise.</returns>
        public bool LoadPlugin(string assemblyPath)
        {
            try
            {
                if (!File.Exists(assemblyPath))
                {
                    OnLoadingFailed($"Plugin assembly not found: {assemblyPath}");
                    return false;
                }

                // Create new load context
                currentContext = new PluginAssemblyLoadContext(assemblyPath);
                
                // Load the assembly
                var assembly = currentContext.LoadFromAssemblyPath(assemblyPath);
                
                // Find the plugin application class
                var pluginType = FindPluginType(assembly);
                if (pluginType == null)
                {
                    OnLoadingFailed($"No IExternalApplication implementation found in: {assemblyPath}");
                    currentContext?.Unload();
                    currentContext = null;
                    return false;
                }

                // Create instance
                currentPlugin = (IExternalApplication)Activator.CreateInstance(pluginType);
                
                return true;
            }
            catch (Exception ex)
            {
                OnLoadingFailed($"Failed to load plugin: {ex.Message}");
                currentContext?.Unload();
                currentContext = null;
                currentPlugin = null;
                return false;
            }
        }

        /// <summary>
        /// Unloads the currently loaded plugin.
        /// </summary>
        /// <returns>True if unloaded successfully, false otherwise.</returns>
        public bool UnloadPlugin()
        {
            try
            {
                if (currentPlugin != null)
                {
                    // Note: We can't call OnShutdown here as it requires UIControlledApplication
                    // The actual plugin cleanup should be handled by Revit itself
                    currentPlugin = null;
                }

                if (currentContext != null)
                {
                    currentContext.Unload();
                    currentContext = null;
                }

                // Force garbage collection to clean up the unloaded context
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                return true;
            }
            catch (Exception ex)
            {
                OnLoadingFailed($"Failed to unload plugin: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reloads the plugin (unload then load).
        /// </summary>
        /// <param name="assemblyPath">Path to the plugin assembly.</param>
        /// <returns>True if reloaded successfully, false otherwise.</returns>
        public bool ReloadPlugin(string assemblyPath)
        {
            // Unload current plugin
            if (!UnloadPlugin())
            {
                return false;
            }

            // Wait a bit for cleanup
            System.Threading.Thread.Sleep(100);

            // Load new plugin
            return LoadPlugin(assemblyPath);
        }

        /// <summary>
        /// Finds the plugin type that implements IExternalApplication.
        /// </summary>
        private Type FindPluginType(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IExternalApplication).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    return type;
                }
            }
            return null;
        }

        /// <summary>
        /// Raises the LoadingFailed event.
        /// </summary>
        private void OnLoadingFailed(string error)
        {
            LoadingFailed?.Invoke(this, error);
        }
    }

    /// <summary>
    /// Custom AssemblyLoadContext for plugin isolation.
    /// </summary>
    internal class PluginAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver resolver;

        public PluginAssemblyLoadContext(string pluginPath) : base(isCollectible: true)
        {
            resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            // Check if this is a system assembly that should be shared
            if (IsSystemAssembly(assemblyName.Name))
            {
                return null; // Let default context handle it
            }

            // Try to resolve the assembly path
            string assemblyPath = resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string libraryPath = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Determines if an assembly should be loaded in the default context.
        /// </summary>
        private bool IsSystemAssembly(string assemblyName)
        {
            // System assemblies that should be shared
            var systemAssemblies = new[]
            {
                "System",
                "mscorlib",
                "netstandard",
                "System.Core",
                "System.Runtime",
                "Microsoft.WindowsDesktop.App",
                "RevitAPI",
                "RevitAPIUI"
            };

            foreach (var sysAssembly in systemAssemblies)
            {
                if (assemblyName.StartsWith(sysAssembly, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}