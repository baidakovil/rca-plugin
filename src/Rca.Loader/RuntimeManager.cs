using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Rca.Loader.Contracts;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;

namespace Rca.Loader
{
    /// <summary>
    /// Manages the runtime lifecycle for hot reloading.
    /// </summary>
    public class RuntimeManager
    {
        private HotReloadAssemblyLoadContext currentContext;
        private IPluginRuntime currentRuntime;
        private WeakReference contextWeakRef;
        private object revitApplication;
        private string currentRuntimePath;

#if DEBUG
        private static int reloadCounter = 0;
#endif

        /// <summary>
        /// Event raised when a reload starts.
        /// </summary>
        public event EventHandler<string> ReloadStarted;

        /// <summary>
        /// Event raised when a reload completes successfully.
        /// </summary>
        public event EventHandler<string> ReloadCompleted;

        /// <summary>
        /// Event raised when a reload fails.
        /// </summary>
        public event EventHandler<string> ReloadFailed;

        /// <summary>
        /// Loads the runtime from the manifest.
        /// </summary>
        /// <param name="application">The Revit application</param>
        public void LoadRuntime(object application)
        {
            revitApplication = application;
            
            var manifest = ReadManifest();
            if (manifest != null && !string.IsNullOrEmpty(manifest.Folder))
            {
                LoadRuntimeFromFolder(manifest.Folder);
            }
            else
            {
                // No manifest found, log this
                LogMessage($"No runtime manifest found at {GetManifestPath()}");
            }
        }

        /// <summary>
        /// Reloads the runtime, optionally from a specific folder.
        /// </summary>
        /// <param name="folder">Optional specific folder to load from</param>
        /// <param name="force">Whether to force reload even if version is same</param>
        public void Reload(string folder = null, bool force = false)
        {
            try
            {
#if DEBUG
                reloadCounter++;
                LogMessage($"Starting reload #{reloadCounter}");
#endif

                ReloadStarted?.Invoke(this, folder ?? "manifest");

                // Determine the folder to load from
                string targetFolder = folder;
                if (string.IsNullOrEmpty(targetFolder))
                {
                    var manifest = ReadManifest();
                    targetFolder = manifest?.Folder;
                }

                if (string.IsNullOrEmpty(targetFolder))
                {
                    throw new InvalidOperationException("No folder specified and no manifest found");
                }

                LoadRuntimeFromFolder(targetFolder);

                ReloadCompleted?.Invoke(this, targetFolder);
                LogMessage($"Runtime reloaded successfully from {targetFolder}");
            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to reload runtime: {ex.Message}";
                ReloadFailed?.Invoke(this, errorMsg);
                LogMessage(errorMsg);
                throw;
            }
        }

        /// <summary>
        /// Unloads the current runtime.
        /// </summary>
        public void UnloadRuntime()
        {
            try
            {
                // Shutdown the current runtime
                if (currentRuntime != null)
                {
                    try
                    {
                        currentRuntime.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Error during runtime shutdown: {ex.Message}");
                    }
                    currentRuntime = null;
                }

                // Unload the context
                if (currentContext != null)
                {
                    contextWeakRef = new WeakReference(currentContext);
                    currentContext.Unload();
                    currentContext = null;

#if DEBUG
                    // Force garbage collection for debugging
                    for (int i = 0; i < 3; i++)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }

                    if (contextWeakRef.IsAlive)
                    {
                        LogMessage("Warning: AssemblyLoadContext is still alive after unload and GC");
                    }
                    else
                    {
                        LogMessage("AssemblyLoadContext successfully collected");
                    }
#endif
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error during runtime unload: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Loads runtime from a specific folder.
        /// </summary>
        private void LoadRuntimeFromFolder(string folder)
        {
            if (!Directory.Exists(folder))
            {
                throw new DirectoryNotFoundException($"Runtime folder not found: {folder}");
            }

            var assemblyPath = Path.Combine(folder, HotReloadConstants.RuntimeAssemblyName);
            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException($"Runtime assembly not found: {assemblyPath}");
            }

            // Skip reload if same path and not forced
            if (currentRuntimePath == assemblyPath)
            {
                LogMessage($"Runtime already loaded from {assemblyPath}");
                return;
            }

            // Unload previous runtime
            UnloadRuntime();

            // Create new context and load assembly
            var contextName = $"RcaRuntime-{DateTime.Now:yyyyMMdd-HHmmss}";
            currentContext = new HotReloadAssemblyLoadContext(contextName);

            var assembly = currentContext.LoadAssemblyFromPath(assemblyPath);
            
            // Find the runtime implementation
            var runtimeType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(IPluginRuntime).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            if (runtimeType == null)
            {
                throw new InvalidOperationException($"No IPluginRuntime implementation found in {assemblyPath}");
            }

            // Create instance and initialize
            currentRuntime = (IPluginRuntime)Activator.CreateInstance(runtimeType);
            currentRuntime.Initialize(revitApplication);
            currentRuntime.OnLoaded();

            currentRuntimePath = assemblyPath;
            LogMessage($"Loaded runtime {currentRuntime.Version} from {assemblyPath}");
        }

        /// <summary>
        /// Reads the runtime manifest.
        /// </summary>
        private RuntimeManifest ReadManifest()
        {
            var manifestPath = GetManifestPath();
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(manifestPath);
                return JsonConvert.DeserializeObject<RuntimeManifest>(json);
            }
            catch (Exception ex)
            {
                LogMessage($"Error reading manifest: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the path to the runtime manifest.
        /// </summary>
        private string GetManifestPath()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var stagingRoot = Path.Combine(appDataPath, HotReloadConstants.DefaultStagingRoot);
            return Path.Combine(stagingRoot, HotReloadConstants.ManifestFileName);
        }

        /// <summary>
        /// Logs a message (placeholder - could integrate with existing logging).
        /// </summary>
        private void LogMessage(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[RuntimeManager] {message}");
            // TODO: Integrate with existing logging system if available
        }
    }
}