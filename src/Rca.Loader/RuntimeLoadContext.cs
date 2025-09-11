using System;
using System.Reflection;
using System.Runtime.Loader;
using System.Linq;
using System.IO;
using System.Diagnostics;

namespace Rca.Loader
{
    /// <summary>
    /// Custom assembly load context for loading runtime assemblies in an isolated, unloadable context.
    /// </summary>
    public class RuntimeLoadContext : AssemblyLoadContext
    {
        private string? runtimePath;
        private object? runtimeInstance;
        private bool disposed = false;
        
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
            Resolving += OnResolving;
            AssemblyLoadContext.Default.Resolving += OnDefaultContextResolving;
        }
        
        private Assembly? OnDefaultContextResolving(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            // Handle Python dependencies that need to be loaded in default context
            var pythonAssemblies = new[]
            {
                "IronPython", "IronPython.Modules", "IronPython.StdLib",
                "Microsoft.Scripting", "Microsoft.Dynamic", "DynamicLanguageRuntime"
            };
            
            if (pythonAssemblies.Contains(assemblyName.Name, StringComparer.OrdinalIgnoreCase))
            {
                return LoadPythonAssemblyFromRuntime(assemblyName.Name);
            }
            
            return null;
        }
        
        private Assembly? LoadPythonAssemblyFromRuntime(string assemblyName)
        {
            if (string.IsNullOrEmpty(runtimePath))
                return null;
            
            var baseDir = Path.GetDirectoryName(runtimePath)!;
            var candidate = Path.Combine(baseDir, assemblyName + ".dll");
            
            if (File.Exists(candidate))
            {
                try
                {
                    return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to load {assemblyName}: {ex.Message}");
                }
            }
            
            return null;
        }
        
        private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            if (string.IsNullOrEmpty(runtimePath))
                return null;
            
            var baseDir = Path.GetDirectoryName(runtimePath)!;
            var assemblyNameOnly = assemblyName.Name ?? "";
            
            // Load these assemblies in default context to avoid collectible assembly issues
            var nonCollectibleAssemblies = new[]
            {
                "Rca.Loader.Contracts", "IronPython", "IronPython.Modules", "IronPython.StdLib",
                "Microsoft.Scripting", "Microsoft.Dynamic", "DynamicLanguageRuntime",
                "System.Numerics", "Microsoft.CSharp", "System.Dynamic.Runtime"
            };
            
            if (nonCollectibleAssemblies.Contains(assemblyNameOnly, StringComparer.OrdinalIgnoreCase))
            {
                return LoadAssemblyInDefaultContext(assemblyNameOnly, baseDir) ?? 
                       LoadAssemblyInCurrentContext(assemblyNameOnly, baseDir);
            }
            
            // For contracts assembly, prefer existing one from default context
            if (assemblyNameOnly == "Rca.Loader.Contracts")
            {
                var existingAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => !a.IsDynamic && a.GetName().Name == assemblyNameOnly);
                if (existingAssembly != null)
                    return existingAssembly;
            }
            
            // Load other assemblies in current context
            return LoadAssemblyInCurrentContext(assemblyNameOnly, baseDir);
        }
        
        private Assembly? LoadAssemblyInDefaultContext(string assemblyName, string baseDir)
        {
            // Check if already loaded
            var existingAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => !a.IsDynamic && 
                    string.Equals(a.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
            
            if (existingAssembly != null)
                return existingAssembly;
            
            // Try to load from runtime directory
            var candidate = Path.Combine(baseDir, assemblyName + ".dll");
            if (File.Exists(candidate))
            {
                try
                {
                    return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
                }
                catch
                {
                    // Fall back to current context
                }
            }
            
            return null;
        }
        
        private Assembly? LoadAssemblyInCurrentContext(string assemblyName, string baseDir)
        {
            var candidate = Path.Combine(baseDir, assemblyName + ".dll");
            if (File.Exists(candidate))
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