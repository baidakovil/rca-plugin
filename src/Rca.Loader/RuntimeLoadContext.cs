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
            Debug.WriteLine("DEBUG: RuntimeLoadContext created as collectible");
            
            // Register resolving event
            Resolving += OnResolving;
            
            // Also register on the default context to catch cross-context references
            AssemblyLoadContext.Default.Resolving += OnDefaultContextResolving;
        }
        
        private Assembly? OnDefaultContextResolving(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            Debug.WriteLine($"DEBUG: Default context resolving: {assemblyName.Name}");
            
            // If this is one of our Python dependencies, try to load it from the runtime directory
            var pythonAssemblies = new[]
            {
                "IronPython",
                "IronPython.Modules", 
                "IronPython.StdLib",
                "Microsoft.Scripting",
                "Microsoft.Dynamic",
                "DynamicLanguageRuntime"
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
            {
                Debug.WriteLine($"DEBUG: Cannot load {assemblyName} - runtime path not set");
                return null;
            }
            
            var baseDir = Path.GetDirectoryName(runtimePath)!;
            var candidate = Path.Combine(baseDir, assemblyName + ".dll");
            
            if (File.Exists(candidate))
            {
                try
                {
                    Debug.WriteLine($"DEBUG: Loading {assemblyName} from runtime directory in default context: {candidate}");
                    return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"DEBUG: Failed to load {assemblyName} from runtime: {ex.Message}");
                }
            }
            else
            {
                Debug.WriteLine($"DEBUG: Assembly file not found: {candidate}");
            }
            
            return null;
        }
        
        private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            Debug.WriteLine($"DEBUG: RuntimeLoadContext resolving: {assemblyName.Name}");
            
            if (string.IsNullOrEmpty(runtimePath))
            {
                Debug.WriteLine("DEBUG: Runtime path not set, cannot resolve");
                return null;
            }
            
            var baseDir = Path.GetDirectoryName(runtimePath)!;
            
            // Special handling for assemblies that should NEVER be loaded as collectible
            // These assemblies need to be loaded in the default context to avoid cross-reference issues
            var nonCollectibleAssemblies = new[]
            {
                "Rca.Loader.Contracts",     // Type identity issues
                "IronPython",               // Python engine assemblies  
                "IronPython.Modules",
                "IronPython.StdLib",
                "Microsoft.Scripting",
                "Microsoft.Dynamic",        // DLR assemblies
                "DynamicLanguageRuntime",
                "System.Numerics",          // Often referenced by IronPython
                "Microsoft.CSharp",         // Dynamic runtime dependencies
                "System.Dynamic.Runtime"    // .NET dynamic runtime
            };
            
            var assemblyNameOnly = assemblyName.Name ?? "";
            
            if (nonCollectibleAssemblies.Any(name => 
                string.Equals(assemblyNameOnly, name, StringComparison.OrdinalIgnoreCase)))
            {
                Debug.WriteLine($"DEBUG: Resolving non-collectible assembly: {assemblyNameOnly}");
                
                // Try to load from the default context first
                var existingAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => !a.IsDynamic && 
                        string.Equals(a.GetName().Name, assemblyNameOnly, StringComparison.OrdinalIgnoreCase));
                
                if (existingAssembly != null)
                {
                    Debug.WriteLine($"DEBUG: Found existing assembly in default context: {assemblyNameOnly}");
                    return existingAssembly;
                }
                
                // If not already loaded, try to load it in the default context
                try
                {
                    // First check if it exists in the runtime directory
                    var candidate = Path.Combine(baseDir, assemblyNameOnly + ".dll");
                    if (File.Exists(candidate))
                    {
                        Debug.WriteLine($"DEBUG: Loading {assemblyNameOnly} from runtime directory in default context: {candidate}");
                        // Load in default context using LoadFrom which respects GAC and already loaded assemblies
                        return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
                    }
                    
                    // Try to resolve from GAC or other standard locations
                    Debug.WriteLine($"DEBUG: Attempting to load {assemblyNameOnly} from default locations");
                    return AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"DEBUG: Failed to load {assemblyNameOnly} in default context: {ex.Message}");
                    
                    // If we can't load in default context, fall back to the runtime directory
                    var candidate = Path.Combine(baseDir, assemblyNameOnly + ".dll");
                    if (File.Exists(candidate))
                    {
                        Debug.WriteLine($"DEBUG: Fallback - loading {assemblyNameOnly} as collectible from: {candidate}");
                        return LoadFromAssemblyPath(candidate);
                    }
                }
            }
            
            // For contracts assembly, prefer the one from the default context
            if (assemblyNameOnly == "Rca.Loader.Contracts")
            {
                // Look for the contracts assembly in the default context
                var contractsAsm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => !a.IsDynamic && a.GetName().Name == assemblyNameOnly);
                
                if (contractsAsm != null)
                {
                    Debug.WriteLine($"DEBUG: Using existing Rca.Loader.Contracts from default context");
                    return contractsAsm;
                }
            }
            
            // Try to load from the runtime directory for other assemblies
            var runtimeCandidate = Path.Combine(baseDir, assemblyNameOnly + ".dll");
            if (File.Exists(runtimeCandidate))
            {
                Debug.WriteLine($"DEBUG: Loading {assemblyNameOnly} as collectible from runtime directory: {runtimeCandidate}");
                return LoadFromAssemblyPath(runtimeCandidate);
            }
            
            Debug.WriteLine($"DEBUG: Could not resolve assembly: {assemblyNameOnly}");
            return null;
        }

        /// <inheritdoc/>
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            Debug.WriteLine($"DEBUG: RuntimeLoadContext.Load called for: {assemblyName.Name}");
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
                // Unregister from default context to avoid memory leaks
                AssemblyLoadContext.Default.Resolving -= OnDefaultContextResolving;
                disposed = true;
            }
            base.Unload();
        }
    }
}