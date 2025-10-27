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
using System.Collections.Generic;

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
                _log.LogTrace("Resolving IRuntimePanelFactory from SharedServiceRegistry");
                // Resolve factory from SharedServiceRegistry (lives in non-collectible Loader context)
                var factory = SharedServiceRegistry.Resolve<IRuntimePanelFactory>();
                if (factory == null)
                {
                    error = "IRuntimePanelFactory not registered - Runtime may not have initialized properly";
                    _log.LogWarning("{Msg}", error);
                    return null;
                }

                _log.LogDebug("Creating panel via factory (type={Type} asm={Asm} loc={Loc})",
                    factory.GetType().FullName,
                    SafeAsmName(factory.GetType().Assembly),
                    SafeAsmLoc(factory.GetType().Assembly));

                var panel = factory.CreatePanel();
                
                if (panel == null)
                {
                    error = "Factory.CreatePanel() returned null";
                    _log.LogWarning("{Msg}", error);
                    return null;
                }

                var pt = panel.GetType();
                var pasm = pt.Assembly;
                _log.LogInformation("Panel created type={Type} asm={Asm} loc={Loc}",
                    pt.FullName,
                    SafeAsmName(pasm),
                    SafeAsmLoc(pasm));

                if (!string.IsNullOrEmpty(CurrentRuntimePath))
                {
                    var expectedDir = Path.GetDirectoryName(CurrentRuntimePath) ?? string.Empty;
                    var actualDir = SafeAsmDir(pasm);
                    _log.LogDebug("Panel assembly directory check expected={Expected} actual={Actual}", expectedDir, actualDir);
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

        private static string SafeAsmName(Assembly asm)
        {
            try { return asm.FullName ?? asm.GetName().Name ?? "(no name)"; } catch { return "(name error)"; }
        }
        private static string SafeAsmLoc(Assembly asm)
        {
            try { return asm.Location; } catch { return "(no location)"; }
        }
        private static string SafeAsmDir(Assembly asm)
        {
            try { return Path.GetDirectoryName(asm.Location) ?? string.Empty; } catch { return string.Empty; }
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

                // Validate atomicity of runtime group before loading
                if (!ValidateRuntimeGroup(folderPath, out var reason))
                {
                    error = reason ?? "Runtime group validation failed";
                    _log.LogWarning("Runtime validation failed: {Reason} opId={Op}", error, opId);
                    return false;
                }

                var runtimeDll = Path.Combine(folderPath, LoaderConstants.RuntimeFileName);
                if (!File.Exists(runtimeDll)) { error = $"Runtime dll not found: {runtimeDll}"; _log.LogWarning("{Msg} opId={Op}", error, opId); return false; }

                _log.LogTrace("Unloading prior runtime if any opId={Op}", opId);
                UnloadRuntime();

                // Preload shared contract assemblies into Default context to avoid collectible duplication issues
                PreloadSharedContracts(folderPath);

                currentContext = new RuntimeLoadContext();
                currentContext.SetRuntimePath(runtimeDll);
                _log.LogDebug("Context created and path set path={Path} opId={Op}", runtimeDll, opId);

                PreloadIronPythonAssemblies(folderPath);

                _log.LogTrace("Loading runtime entry assembly opId={Op}", opId);
                var assembly = currentContext.LoadFromAssemblyPath(runtimeDll);
                _log.LogTrace("Finding RuntimeEntry type opId={Op}", opId);
                var runtimeType = FindRuntimeEntryType(assembly);
                if (runtimeType == null) { error = "RuntimeEntry class not found"; _log.LogWarning("{Msg} opId={Op}", error, opId); return false; }

                _log.LogTrace("Creating RuntimeEntry instance opId={Op}", opId);
                var instance = Activator.CreateInstance(runtimeType);
                if (instance == null) { error = "Failed to create runtime instance"; _log.LogWarning("{Msg} opId={Op}", error, opId); return false; }

                var initMethod = runtimeType.GetMethod("Initialize");
                if (initMethod == null) { error = "Initialize method not found on RuntimeEntry"; _log.LogWarning("{Msg} opId={Op}", error, opId); return false; }

                currentContext.SetRuntimeInstance(instance);
                _log.LogTrace("Invoking Initialize on RuntimeEntry opId={Op}", opId);
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

        private void PreloadSharedContracts(string folder)
        {
            try
            {
                // Rca.Contracts is shared between Loader and Runtime; prefer single copy in Default context
                var shared = new[] { "Rca.Contracts.dll" };
                foreach (var dll in shared)
                {
                    var name = Path.GetFileNameWithoutExtension(dll);
                    var already = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => !a.IsDynamic && string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase));
                    if (already != null)
                    {
                        _log.LogTrace("Shared contract already loaded name={Name}", name);
                        continue;
                    }
                    var path = Path.Combine(folder, dll);
                    if (!File.Exists(path)) { _log.LogDebug("Shared contract not found in runtime folder dll={Dll}", dll); continue; }
                    try
                    {
                        AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
                        _log.LogDebug("Preloaded shared contract into Default ALC dll={Dll}", dll);
                    }
                    catch (Exception ex)
                    {
                        _log.LogDebug(ex, "Failed to preload shared contract dll={Dll}", dll);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "Error during PreloadSharedContracts");
            }
        }

        private bool ValidateRuntimeGroup(string folderPath, out string? reason)
        {
            reason = null;
            try
            {
                var hashes = new List<string>();
                foreach (var dll in LoaderConstants.RuntimeAssemblies)
                {
                    var path = Path.Combine(folderPath, dll);
                    if (!File.Exists(path))
                    {
                        reason = $"Missing runtime assembly: {dll}";
                        _log.LogWarning("{Reason}", reason);
                        return false;
                    }
                    var hash = AttributeMetadataLoader.TryGetFromFile(path, BuildConstants.SourceHashMetadataKey);
                    _log.LogTrace("Read SourceHash from {Dll}: {Hash}", dll, hash);
                    if (string.IsNullOrEmpty(hash) || hash == AttributeMetadataLoader.MissingMarker)
                    {
                        reason = $"Missing or empty SourceHash in {dll}";
                        _log.LogWarning("{Reason}", reason);
                        return false;
                    }
                    hashes.Add(hash);
                }
                var shortHashes = hashes.Select(h => (h ?? string.Empty).Trim()).Select(h => h.Length > 8 ? h[..8] : h).ToList();
                var allEqual = shortHashes.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1;
                if (!allEqual)
                {
                    reason = $"Runtime group hash mismatch: {string.Join(", ", shortHashes)}";
                    _log.LogWarning("{Reason}", reason);
                    return false;
                }
                _log.LogDebug("Runtime group validation passed hash={Hash}", shortHashes.First());
                return true;
            }
            catch (Exception ex)
            {
                reason = ex.Message;
                _log.LogError(ex, "Error validating runtime group");
                return false;
            }
        }

        private Type? FindRuntimeEntryType(Assembly assembly)
        {
            try
            {
                _log.LogTrace("Scanning types for RuntimeEntry in {Asm}", assembly.FullName);
                return assembly.GetTypes().FirstOrDefault(t => t.Name == "RuntimeEntry" && !t.IsAbstract);
            }
            catch (ReflectionTypeLoadException rtle)
            {
                _log.LogDebug(rtle, "ReflectionTypeLoadException while finding RuntimeEntry");
                return rtle.Types?.FirstOrDefault(t => t != null && t.Name == "RuntimeEntry" && !t.IsAbstract);
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "Error while finding RuntimeEntry type");
                return null;
            }
        }

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
                if (!File.Exists(assemblyPath)) { _log.LogTrace("Python assembly not found {Asm}", assemblyFile); continue; }
                try
                {
                    var assemblyName = Path.GetFileNameWithoutExtension(assemblyFile);
                    var existing = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => !a.IsDynamic && string.Equals(a.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
                        _log.LogDebug("Preloaded python assembly {Asm}", assemblyFile);
                    }
                    else
                    {
                        _log.LogTrace("Python assembly already loaded {Asm}", assemblyFile);
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
            if (currentContext == null) { _log.LogTrace("UnloadRuntime skipped - no context"); return; }
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
                        _log.LogTrace("Shutdown invoked on runtime instance opId={Op}", opId);
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
                    _log.LogTrace("Panel host content cleared opId={Op}", opId);
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
            if (!Directory.Exists(LoaderConstants.RevitAddinsDir))
            {
                error = $"Revit addins directory not found: {LoaderConstants.RevitAddinsDir}";
                _log.LogWarning("{Msg}", error);
                return false;
            }
            var latest = Directory.GetDirectories(LoaderConstants.RevitAddinsDir).OrderByDescending(d => d).FirstOrDefault();
            _log.LogDebug("ReloadLatest selected dir={Dir}", latest);
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
