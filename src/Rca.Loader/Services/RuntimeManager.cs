using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Runtime.Loader;
using Autodesk.Revit.UI;
using Rca.Loader.Contracts;
using Rca.Loader.Infrastructure;
using Rca.Contracts.Infrastructure;
using Rca.Loader.Logging;
using Microsoft.Extensions.Logging;

namespace Rca.Loader.Services
{
    /// <summary>
    /// Manages loading, unloading, and interactions with the runtime assembly.
    /// Provides detailed structured logs for every lifecycle operation.
    /// </summary>
    public class RuntimeManager : IRuntimeManager
    {
        private RuntimeLoadContext? currentContext;
        private object? currentRuntimeInstance;
        private readonly ILogger _log = LoaderLog.GetLogger<RuntimeManager>();

        /// <summary>
        /// Gets the currently loaded runtime context, if any.
        /// </summary>
        public RuntimeLoadContext? CurrentContext => currentContext;

        /// <summary>
        /// Gets whether a runtime is currently loaded.
        /// </summary>
        public bool IsRuntimeLoaded => currentRuntimeInstance != null;

        /// <summary>
        /// Gets the path of the currently loaded runtime, if any.
        /// </summary>
        public string CurrentRuntimePath => currentContext?.RuntimePath ?? string.Empty;

        /// <summary>
        /// Contract-compatible CreateRuntimeDockableContent without UIApplication parameter.
        /// Uses SharedServiceRegistry to resolve factory across AssemblyLoadContext boundary.
        /// </summary>
        /// <param name="error">Out error message.</param>
        /// <returns>FrameworkElement or null.</returns>
        public FrameworkElement? CreateRuntimeDockableContent(out string? error)
        {
            error = null;

            if (currentContext == null)
            {
                error = "Runtime not loaded";
                _log.LogWarning("CreateRuntimeDockableContent called but runtime not loaded");
                return null;
            }

            try
            {
                // Resolve factory from SharedServiceRegistry (lives in non-collectible Loader context)
                var factory = SharedServiceRegistry.Resolve<IRuntimePanelFactory>();
                if (factory == null)
                {
                    error = "IRuntimePanelFactory not registered - Runtime may not have initialized properly";
                    _log.LogWarning("{Msg}", error);
                    return null;
                }

                _log.LogDebug("Creating panel via factory (type={Type})", factory.GetType().FullName);
                var panel = factory.CreatePanel();
                
                if (panel == null)
                {
                    error = "Factory.CreatePanel() returned null";
                    _log.LogWarning("{Msg}", error);
                    return null;
                }

                _log.LogInformation("Panel created successfully via factory");
                return panel;
            }
            catch (Exception ex)
            {
                error = $"Error creating dockable content: {ex.Message}";
                _log.LogError(ex, "Error creating dockable content");
                return null;
            }
        }

        /// <summary>
        /// Reloads the runtime from a specified folder path.
        /// </summary>
        /// <param name="folderPath">Path to the folder containing the runtime DLL.</param>
        /// <param name="error">Error message if load fails.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public bool ReloadRuntime(string? folderPath, out string? error)
        {
            var opId = Guid.NewGuid().ToString("N");
            _log.LogInformation("ReloadRuntime start opId={Op} path={Path}", opId, folderPath);
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath)) { error = "Folder path missing"; _log.LogWarning("{Msg} opId={Op}", error, opId); return false; }
                var runtimeDll = Path.Combine(folderPath, LoaderConstants.RuntimeFileName);
                if (!File.Exists(runtimeDll)) { error = $"Runtime dll not found: {runtimeDll}"; _log.LogWarning("{Msg} opId={Op}", error, opId); return false; }

                UnloadRuntime();
                currentContext = new RuntimeLoadContext();
                currentContext.SetRuntimePath(runtimeDll);
                PreloadIronPythonAssemblies(folderPath);

                // Load and initialize the runtime
                var assembly = currentContext.LoadFromAssemblyPath(runtimeDll);
                var runtimeType = FindRuntimeEntryType(assembly);
                if (runtimeType == null) { error = "RuntimeEntry class not found"; _log.LogWarning("{Msg} opId={Op}", error, opId); return false; }

                var instance = Activator.CreateInstance(runtimeType);
                if (instance == null) { error = "Failed to create runtime instance"; _log.LogWarning("{Msg} opId={Op}", error, opId); return false; }

                var initMethod = runtimeType.GetMethod("Initialize");
                if (initMethod == null) { error = "Initialize method not found on RuntimeEntry"; _log.LogWarning("{Msg} opId={Op}", error, opId); return false; }

                currentContext.SetRuntimeInstance(instance);
                initMethod.Invoke(instance, null);
                currentRuntimeInstance = instance;
                error = null;
                _log.LogInformation("ReloadRuntime success opId={Op}", opId);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                _log.LogError(ex, "ReloadRuntime failed opId={Op}", opId);
                return false;
            }
        }

        private Type? FindRuntimeEntryType(Assembly assembly) => assembly.GetTypes().FirstOrDefault(t => t.Name == "RuntimeEntry" && !t.IsAbstract);

        /// <summary>
        /// Pre-loads IronPython assemblies in the default context to avoid collectible assembly issues.
        /// </summary>
        /// <param name="runtimeFolder">The runtime folder containing the assemblies.</param>
        private void PreloadIronPythonAssemblies(string runtimeFolder)
        {
            var pythonAssemblies = new[] { "Microsoft.Dynamic.dll", "Microsoft.Scripting.dll", "IronPython.dll", "IronPython.Modules.dll" };
            foreach (var assemblyFile in pythonAssemblies)
            {
                var assemblyPath = Path.Combine(runtimeFolder, assemblyFile);
                if (!File.Exists(assemblyPath)) continue;
                try
                {
                    var assemblyName = Path.GetFileNameWithoutExtension(assemblyFile);
                    var existing = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => !a.IsDynamic && string.Equals(a.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
                        _log.LogDebug("Preloaded python assembly {Asm}", assemblyFile);
                    }
                }
                catch (Exception ex)
                {
                    _log.LogDebug(ex, "Failed preloading python assembly {Asm}", assemblyFile);
                }
            }
        }

        /// <summary>
        /// Unloads the current runtime, if loaded.
        /// </summary>
        public void UnloadRuntime()
        {
            if (currentContext == null) return;
            var opId = Guid.NewGuid().ToString("N");
            _log.LogInformation("UnloadRuntime start opId={Op} path={Path}", opId, currentContext.RuntimePath);
            try
            {
                try
                {
                    if (currentContext.RuntimeInstance != null)
                    {
                        var rtType = currentContext.RuntimeInstance.GetType();
                        var shutdown = rtType.GetMethod("Shutdown");
                        shutdown?.Invoke(currentContext.RuntimeInstance, null);
                    }
                }
                catch (Exception exShutdown)
                {
                    _log.LogDebug(exShutdown, "Runtime shutdown hook failed opId={Op}", opId);
                }

                currentRuntimeInstance = null;

                try
                {
                    var host = LoaderApp.Instance?.PanelHost;
                    host?.SetContent(null);
                }
                catch (Exception exPanel)
                {
                    _log.LogDebug(exPanel, "Failed clearing panel host content opId={Op}", opId);
                }

                currentContext.Unload();
                currentContext = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                _log.LogInformation("UnloadRuntime complete opId={Op}", opId);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "UnloadRuntime failure opId={Op}", opId);
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
                _log.LogWarning("{Msg}", error);
                return false;
            }
            var latest = Directory.GetDirectories(LoaderConstants.RuntimeDeployRoot).OrderByDescending(d => d).FirstOrDefault();
            if (latest == null)
            {
                error = "No runtime versions found";
                _log.LogWarning("{Msg}", error);
                return false;
            }
            return ReloadRuntime(latest, out error);
        }
    }
}
