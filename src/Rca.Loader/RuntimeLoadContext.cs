using System;
using System.Reflection;
using System.Runtime.Loader;
using System.Linq;

namespace Rca.Loader
{
    /// <summary>
    /// Custom assembly load context for loading runtime assemblies in an isolated, unloadable context.
    /// </summary>
    public class RuntimeLoadContext : AssemblyLoadContext
    {
        private string? runtimePath;
        private object? runtimeInstance;
        
        /// <summary>
        /// Gets the path to the runtime assembly.
        /// </summary>
        public string RuntimePath => runtimePath ?? "";
        
        /// <summary>
        /// Gets the runtime instance.
        /// </summary>
        public object? RuntimeInstance => runtimeInstance;
        
        /// <summary>
        /// Sets the path to the runtime assembly.
        /// </summary>
        public void SetRuntimePath(string path)
        {
            runtimePath = path;
        }
        
        /// <summary>
        /// Sets the runtime instance.
        /// </summary>
        public void SetRuntimeInstance(object instance)
        {
            runtimeInstance = instance;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeLoadContext"/> class.
        /// </summary>
        public RuntimeLoadContext() : base(isCollectible: true)
        {
            // Register resolving event
            Resolving += OnResolving;
        }
        
        private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            if (string.IsNullOrEmpty(runtimePath))
                return null;
            
            var baseDir = System.IO.Path.GetDirectoryName(runtimePath)!;
            
            // Special handling for the Contracts assembly to avoid type identity issues
            if (assemblyName.Name == "Rca.Loader.Contracts")
            {
                // Look for the contracts assembly in the default context
                var contractsAsm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => !a.IsDynamic && a.GetName().Name == assemblyName.Name);
                
                if (contractsAsm != null)
                    return contractsAsm;
            }
            
            // Try to load from the runtime directory
            var candidate = System.IO.Path.Combine(baseDir, assemblyName.Name + ".dll");
            if (System.IO.File.Exists(candidate))
            {
                return LoadFromAssemblyPath(candidate);
            }
            
            return null;
        }

        /// <inheritdoc/>
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Prefer to use the Resolving event handler
            return null;
        }
    }
}