using Autodesk.Revit.UI;
using Rca.Loader.Contracts;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Rca.Loader.Services
{
    /// <summary>
    /// Manages the lifecycle of the hot-reloadable runtime assembly.
    /// Handles loading, unloading, and reloading of the dynamic runtime.
    /// </summary>
    public class RuntimeManager
    {
        private HotReloadAssemblyLoadContext currentContext;
        private IPluginRuntime currentRuntime;
        private WeakReference currentContextWeakRef;
        private UIControlledApplication uiApplication;

        /// <summary>
        /// Gets the path to the staging directory for runtime assemblies.
        /// </summary>
        public string StagingPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            HotReloadConstants.StagingDirectory);

        /// <summary>
        /// Gets the path to the current runtime manifest file.
        /// </summary>
        public string ManifestPath => Path.Combine(StagingPath, HotReloadConstants.ManifestFileName);

        /// <summary>
        /// Loads the initial runtime if a manifest exists.
        /// </summary>
        public void LoadInitialRuntime(UIControlledApplication application)
        {
            uiApplication = application;

            if (File.Exists(ManifestPath))
            {
                var success = Reload();
                if (!success)
                {
                    TaskDialog.Show("RCA Loader", "No valid runtime found. Build the Rca.Runtime project to enable hot reload.");
                }
            }
            else
            {
                TaskDialog.Show("RCA Loader", "No runtime manifest found. Build the Rca.Runtime project to enable hot reload.");
            }
        }

        /// <summary>
        /// Reloads the runtime from the current manifest.
        /// </summary>
        /// <param name="force">Force reload even if no new version is available</param>
        /// <returns>True if reload was successful</returns>
        public bool Reload(bool force = false)
        {
            try
            {
                // Read the current manifest
                if (!File.Exists(ManifestPath))
                {
                    LogError("Runtime manifest not found. Build the Rca.Runtime project first.");
                    return false;
                }

                var manifestJson = File.ReadAllText(ManifestPath);
                var manifest = JsonSerializer.Deserialize<RuntimeManifest>(manifestJson);

                if (string.IsNullOrEmpty(manifest?.Folder) || string.IsNullOrEmpty(manifest?.Assembly))
                {
                    LogError("Invalid runtime manifest format.");
                    return false;
                }

                var assemblyPath = Path.Combine(StagingPath, manifest.Folder, manifest.Assembly);
                if (!File.Exists(assemblyPath))
                {
                    LogError($"Runtime assembly not found: {assemblyPath}");
                    return false;
                }

                LogInfo($"Loading runtime from: {assemblyPath}");

                // Unload current runtime
                UnloadCurrentRuntime();

                // Create new context and load assembly
                currentContext = new HotReloadAssemblyLoadContext();
                currentContextWeakRef = new WeakReference(currentContext);

                var assembly = currentContext.LoadFromAssemblyPath(assemblyPath);
                
                // Find the runtime implementation
                var runtimeType = assembly.GetTypes()
                    .FirstOrDefault(t => typeof(IPluginRuntime).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                if (runtimeType == null)
                {
                    LogError("No IPluginRuntime implementation found in runtime assembly.");
                    UnloadCurrentRuntime();
                    return false;
                }

                // Create and initialize runtime instance
                currentRuntime = (IPluginRuntime)Activator.CreateInstance(runtimeType);
                currentRuntime.Initialize(uiApplication);
                currentRuntime.OnLoaded();

                LogInfo($"Runtime loaded successfully. Version: {currentRuntime.Version}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to reload runtime: {ex.Message}");
                UnloadCurrentRuntime();
                return false;
            }
        }

        /// <summary>
        /// Unloads the current runtime and triggers garbage collection.
        /// </summary>
        public void UnloadCurrentRuntime()
        {
            try
            {
                // Shutdown current runtime
                if (currentRuntime != null)
                {
                    try
                    {
                        currentRuntime.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error during runtime shutdown: {ex.Message}");
                    }
                    currentRuntime = null;
                }

                // Unload context
                if (currentContext != null)
                {
                    currentContext.Unload();
                    currentContext = null;
                }

                // Force garbage collection to ensure assembly unloading
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

#if DEBUG
                // Check if context was actually collected (debug only)
                Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    if (currentContextWeakRef != null && !currentContextWeakRef.IsAlive)
                    {
                        LogInfo("AssemblyLoadContext successfully collected.");
                    }
                    else
                    {
                        LogWarning("AssemblyLoadContext may not have been collected.");
                    }
                });
#endif
            }
            catch (Exception ex)
            {
                LogError($"Error during runtime unload: {ex.Message}");
            }
        }

        private void LogInfo(string message)
        {
            Console.WriteLine($"[RCA Loader] {message}");
        }

        private void LogWarning(string message)
        {
            Console.WriteLine($"[RCA Loader] WARNING: {message}");
        }

        private void LogError(string message)
        {
            Console.WriteLine($"[RCA Loader] ERROR: {message}");
        }
    }
}