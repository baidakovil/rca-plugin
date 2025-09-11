using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Runtime.Loader;
using Rca.Loader.Contracts;
using System.Diagnostics;

namespace Rca.Loader
{
    /// <summary>
    /// Manages loading, unloading, and interactions with the runtime assembly.
    /// </summary>
    public class RuntimeManager
    {
        private RuntimeLoadContext? currentContext;
        private IRuntime? currentRuntime;
        
        /// <summary>
        /// Gets the currently loaded runtime context, if any.
        /// </summary>
        public RuntimeLoadContext? CurrentContext => currentContext;
        
        /// <summary>
        /// Gets whether a runtime is currently loaded.
        /// </summary>
        public bool IsRuntimeLoaded => currentRuntime != null;
        
        /// <summary>
        /// Gets the path of the currently loaded runtime, if any.
        /// </summary>
        public string CurrentRuntimePath => currentContext?.RuntimePath ?? string.Empty;

        /// <summary>
        /// Reloads the runtime from a specified folder path.
        /// </summary>
        /// <param name="folderPath">Path to the folder containing the runtime DLL.</param>
        /// <param name="error">Error message if load fails.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public bool ReloadRuntime(string? folderPath, out string? error)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath)) { error = "Folder path missing"; return false; }
                var runtimeDll = Path.Combine(folderPath, LoaderConstants.RuntimeFileName);
                if (!File.Exists(runtimeDll)) { error = $"Runtime dll not found: {runtimeDll}"; return false; }

                Debug.WriteLine($"DEBUG: RuntimeManager.ReloadRuntime called with: {folderPath}");
                
                UnloadRuntime();

                // Create a new context for loading the runtime
                currentContext = new RuntimeLoadContext();
                
                // Set the runtime path early so assembly resolution can work
                currentContext.SetRuntimePath(runtimeDll);
                
                // Pre-load IronPython assemblies in the default context to avoid collectible assembly issues
                PreloadIronPythonAssemblies(folderPath);
                
                // Load the runtime DLL into our context
                var asm = currentContext.LoadFromAssemblyPath(runtimeDll);
                
                // Look for RuntimeEntry type directly by name
                Type? rtType = null;
                foreach (var type in asm.GetTypes())
                {
                    if (type.Name == "RuntimeEntry" && !type.IsAbstract)
                    {
                        rtType = type;
                        break;
                    }
                }
                
                if (rtType == null) 
                { 
                    // Log all available types for debugging
                    var allTypes = string.Join(", ", asm.GetTypes().Select(t => t.FullName));
                    error = $"RuntimeEntry class not found. Available types: {allTypes}"; 
                    return false; 
                }
                
                // Create an instance of RuntimeEntry
                var instance = Activator.CreateInstance(rtType);
                
                // Use reflection to invoke methods on the RuntimeEntry instance
                var initMethod = rtType.GetMethod("Initialize");
                if (initMethod == null)
                {
                    error = "Initialize method not found on RuntimeEntry";
                    return false;
                }
                
                // Check for null before setting the runtime instance
                if (instance == null)
                {
                    error = "Failed to create runtime instance";
                    return false;
                }
                
                currentContext.SetRuntimeInstance(instance);
                
                // Call Initialize
                initMethod.Invoke(instance, null);
                
                Debug.WriteLine("DEBUG: Runtime loaded successfully");
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DEBUG: RuntimeManager.ReloadRuntime failed: {ex}");
                error = ex.ToString();
                return false;
            }
        }
        
        /// <summary>
        /// Pre-loads IronPython assemblies in the default context to avoid collectible assembly issues.
        /// </summary>
        /// <param name="runtimeFolder">The runtime folder containing the assemblies.</param>
        private void PreloadIronPythonAssemblies(string runtimeFolder)
        {
            Debug.WriteLine("DEBUG: Pre-loading IronPython assemblies in default context");
            
            var pythonAssemblies = new[]
            {
                "Microsoft.Dynamic.dll",
                "Microsoft.Scripting.dll", 
                "IronPython.dll",
                "IronPython.Modules.dll"
            };
            
            foreach (var assemblyFile in pythonAssemblies)
            {
                var assemblyPath = Path.Combine(runtimeFolder, assemblyFile);
                if (File.Exists(assemblyPath))
                {
                    try
                    {
                        Debug.WriteLine($"DEBUG: Pre-loading {assemblyFile} in default context");
                        
                        // Check if it's already loaded
                        var assemblyName = Path.GetFileNameWithoutExtension(assemblyFile);
                        var existingAssembly = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(a => !a.IsDynamic && 
                                string.Equals(a.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
                        
                        if (existingAssembly != null)
                        {
                            Debug.WriteLine($"DEBUG: {assemblyFile} already loaded in default context");
                            continue;
                        }
                        
                        // Load in default context
                        var loadedAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
                        Debug.WriteLine($"DEBUG: Successfully pre-loaded {assemblyFile}: {loadedAssembly.FullName}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"DEBUG: Failed to pre-load {assemblyFile}: {ex.Message}");
                        // Continue with other assemblies even if one fails
                    }
                }
                else
                {
                    Debug.WriteLine($"DEBUG: Assembly file not found: {assemblyPath}");
                }
            }
        }

        /// <summary>
        /// Unloads the current runtime, if loaded.
        /// </summary>
        public void UnloadRuntime()
        {
            try 
            {
                if (currentContext?.RuntimeInstance != null)
                {
                    var rtType = currentContext.RuntimeInstance.GetType();
                    var shutdownMethod = rtType.GetMethod("Shutdown");
                    shutdownMethod?.Invoke(currentContext.RuntimeInstance, null);
                }
            } 
            catch { }
            
            currentRuntime = null;
            
            if (currentContext != null)
            {
                currentContext.Unload();
                currentContext = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        /// <summary>
        /// Shows the standalone window from the loaded runtime.
        /// </summary>
        /// <param name="error">Error message if operation fails.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public bool ShowStandaloneWindow(out string? error)
        {
            if (currentContext == null)
            {
                error = "Runtime not loaded";
                return false;
            }
            try
            {
                var asm = currentContext.Assemblies.FirstOrDefault(a => 
                    !a.IsDynamic && 
                    string.Equals(Path.GetFileName(a.Location), LoaderConstants.RuntimeFileName, StringComparison.OrdinalIgnoreCase));
                    
                if (asm == null)
                {
                    error = "Runtime assembly not found in context";
                    return false;
                }
                
                // Find the standalone window type by name
                Type? winType = null;
                foreach (var type in asm.GetTypes())
                {
                    if (type.Name == "RcaStandaloneWindow" && typeof(Window).IsAssignableFrom(type))
                    {
                        winType = type;
                        break;
                    }
                }
                
                if (winType == null)
                {
                    // Log all available window types
                    var availableWindowTypes = string.Join(", ", 
                        asm.GetTypes()
                        .Where(t => typeof(Window).IsAssignableFrom(t))
                        .Select(t => t.FullName));
                    
                    error = $"RcaStandaloneWindow type not found. Available window types: {availableWindowTypes}";
                    return false;
                }
                
                if (Activator.CreateInstance(winType) is Window window)
                {
                    window.Show();
                    error = null;
                    return true;
                }
                error = "Failed to create window instance";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
        }

        /// <summary>
        /// Reloads the latest version of the runtime from the deploy root.
        /// </summary>
        /// <param name="error">Error message if operation fails.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public bool ReloadLatest(out string? error)
        {
            if (!Directory.Exists(LoaderConstants.RuntimeDeployRoot))
            {
                error = $"Runtime root not found: {LoaderConstants.RuntimeDeployRoot}";
                return false;
            }
            var latest = Directory.GetDirectories(LoaderConstants.RuntimeDeployRoot)
                .OrderByDescending(d => d)
                .FirstOrDefault();
            if (latest == null)
            {
                error = "No runtime versions found";
                return false;
            }
            return ReloadRuntime(latest, out error);
        }
    }
}