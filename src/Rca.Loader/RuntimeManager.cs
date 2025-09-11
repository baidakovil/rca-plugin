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
                if (string.IsNullOrWhiteSpace(folderPath)) 
                { 
                    error = "Folder path missing"; 
                    return false; 
                }
                
                var runtimeDll = Path.Combine(folderPath, LoaderConstants.RuntimeFileName);
                if (!File.Exists(runtimeDll)) 
                { 
                    error = $"Runtime dll not found: {runtimeDll}"; 
                    return false; 
                }

                UnloadRuntime();

                // Create new context and set runtime path for assembly resolution
                currentContext = new RuntimeLoadContext();
                currentContext.SetRuntimePath(runtimeDll);
                
                // Pre-load IronPython assemblies to avoid collectible assembly issues
                PreloadIronPythonAssemblies(folderPath);
                
                // Load and initialize the runtime
                var assembly = currentContext.LoadFromAssemblyPath(runtimeDll);
                var runtimeType = FindRuntimeEntryType(assembly);
                
                if (runtimeType == null)
                {
                    error = "RuntimeEntry class not found";
                    return false;
                }
                
                var instance = Activator.CreateInstance(runtimeType);
                if (instance == null)
                {
                    error = "Failed to create runtime instance";
                    return false;
                }
                
                var initMethod = runtimeType.GetMethod("Initialize");
                if (initMethod == null)
                {
                    error = "Initialize method not found on RuntimeEntry";
                    return false;
                }
                
                currentContext.SetRuntimeInstance(instance);
                initMethod.Invoke(instance, null);
                
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
        }
        
        private Type? FindRuntimeEntryType(Assembly assembly)
        {
            return assembly.GetTypes()
                .FirstOrDefault(type => type.Name == "RuntimeEntry" && !type.IsAbstract);
        }
        
        /// <summary>
        /// Pre-loads IronPython assemblies in the default context to avoid collectible assembly issues.
        /// </summary>
        /// <param name="runtimeFolder">The runtime folder containing the assemblies.</param>
        private void PreloadIronPythonAssemblies(string runtimeFolder)
        {
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
                        var assemblyName = Path.GetFileNameWithoutExtension(assemblyFile);
                        
                        // Check if already loaded
                        var existingAssembly = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(a => !a.IsDynamic && 
                                string.Equals(a.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
                        
                        if (existingAssembly == null)
                        {
                            AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to pre-load {assemblyFile}: {ex.Message}");
                    }
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
                var assembly = currentContext.Assemblies.FirstOrDefault(a => 
                    !a.IsDynamic && 
                    string.Equals(Path.GetFileName(a.Location), LoaderConstants.RuntimeFileName, StringComparison.OrdinalIgnoreCase));
                    
                if (assembly == null)
                {
                    error = "Runtime assembly not found in context";
                    return false;
                }
                
                var windowType = assembly.GetTypes()
                    .FirstOrDefault(t => t.Name == "RcaStandaloneWindow" && typeof(Window).IsAssignableFrom(t));
                
                if (windowType == null)
                {
                    error = "RcaStandaloneWindow type not found";
                    return false;
                }
                
                if (Activator.CreateInstance(windowType) is Window window)
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