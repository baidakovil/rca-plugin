#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Rca.Loader.Infrastructure
{
    /// <summary>
    /// Custom assembly load context for loading runtime assemblies in an isolated, unloadable context.
    /// </summary>
    public class RuntimeLoadContext : AssemblyLoadContext
    {
        private AssemblyLoadService? assemblyLoader;
        private string? runtimePath;
        private object? runtimeInstance;
        private bool disposed = false;
        
        /// <summary>
        /// Gets the path to the runtime assembly.
        /// </summary>
        public string RuntimePath => runtimePath ?? string.Empty;
        
        /// <summary>
        /// Gets the runtime instance.
        /// </summary>
        public object? RuntimeInstance => runtimeInstance;
        
        /// <summary>
        /// Sets the path to the runtime assembly.
        /// </summary>
        /// <param name="path">The path to the runtime assembly.</param>
        public void SetRuntimePath(string path)
        {
            runtimePath = path;
            
            // Update the assembly loader with the new base directory
            var baseDirectory = !string.IsNullOrEmpty(path) ? Path.GetDirectoryName(path) : null;
            assemblyLoader = new AssemblyLoadService(this, baseDirectory);
        }
        
        /// <summary>
        /// Sets the runtime instance.
        /// </summary>
        /// <param name="instance">The runtime instance to set.</param>
        public void SetRuntimeInstance(object instance) => runtimeInstance = instance;

        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeLoadContext"/> class.
        /// </summary>
        public RuntimeLoadContext() : base(isCollectible: true)
        {
            assemblyLoader = new AssemblyLoadService(this);
            Resolving += OnResolving;
            AssemblyLoadContext.Default.Resolving += OnDefaultContextResolving;
        }
        
        private Assembly? OnDefaultContextResolving(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            var assemblyNameValue = assemblyName.Name;
            if (assemblyNameValue == null || assemblyLoader == null)
                return null;

            // Handle Python dependencies that need to be loaded in default context
            if (AssemblyLoadConstants.PythonAssemblies.Contains(assemblyNameValue, StringComparer.OrdinalIgnoreCase))
            {
                return assemblyLoader.TryLoad(AssemblyLoadStrategy.RuntimeToDefault, assemblyNameValue);
            }
            
            return null;
        }
        
        private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            var assemblyNameOnly = assemblyName.Name ?? string.Empty;
            if (string.IsNullOrEmpty(assemblyNameOnly) || assemblyLoader == null)
                return null;
            
            // Load these assemblies in default context to avoid collectible assembly issues
            if (AssemblyLoadConstants.NonCollectibleAssemblies.Contains(assemblyNameOnly, StringComparer.OrdinalIgnoreCase))
            {
                return assemblyLoader.TryLoad(AssemblyLoadStrategy.DefaultContext, assemblyNameOnly) ?? 
                       assemblyLoader.TryLoad(AssemblyLoadStrategy.CurrentContext, assemblyNameOnly);
            }
            
            // For contracts assembly, prefer existing one from default context
            if (string.Equals(assemblyNameOnly, AssemblyLoadConstants.LoaderContractsAssembly, StringComparison.OrdinalIgnoreCase))
            {
                var existingAssembly = assemblyLoader.FindExistingInDefaultContext(assemblyNameOnly);
                if (existingAssembly != null)
                    return existingAssembly;
            }
            
            // Load other assemblies in current context
            return assemblyLoader.TryLoad(AssemblyLoadStrategy.CurrentContext, assemblyNameOnly);
        }

        /// <inheritdoc/>
        protected override Assembly? Load(AssemblyName assemblyName) => null;
        
        /// <summary>
        /// Cleans up resources and unregisters event handlers.
        /// </summary>
        public new void Unload()
        {
            if (!disposed)
            {
                AssemblyLoadContext.Default.Resolving -= OnDefaultContextResolving;
                disposed = true;
            }
            base.Unload();
        }
    }
}