using Autodesk.Revit.UI;
using Rca.Loader.Contracts;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading;

namespace Rca.Loader
{
    /// <summary>
    /// Manages runtime loading and unloading for hot reload functionality.
    /// </summary>
    public class RuntimeManager
    {
        private HotReloadAssemblyLoadContext currentContext;
        private IPluginRuntime currentRuntime;
        private UIControlledApplication revitApplication;
        private WeakReference contextWeakRef;

        /// <summary>
        /// Loads the initial runtime if available.
        /// </summary>
        /// <param name="application">The Revit UI controlled application</param>
        public void LoadInitialRuntime(object application)
        {
            if (!(application is UIControlledApplication uiApp))
            {
                throw new ArgumentException("Application must be a UIControlledApplication", nameof(application));
            }

            revitApplication = uiApp;
            var manifestPath = GetCurrentManifestPath();
            
            if (File.Exists(manifestPath))
            {
                try
                {
                    var manifest = ReadManifest(manifestPath);
                    if (manifest != null)
                    {
                        LoadRuntime(manifest.Folder);
                    }
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("RCA Loader Warning", $"Failed to load initial runtime: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Reloads the runtime from the specified folder or current manifest.
        /// </summary>
        /// <param name="folderOverride">Optional folder override</param>
        /// <param name="force">Whether to force reload even if no changes detected</param>
        public void Reload(string folderOverride = null, bool force = false)
        {
            try
            {
                SendEvent("RELOAD_START", new { force, folderOverride });

                string targetFolder = folderOverride;
                if (string.IsNullOrEmpty(targetFolder))
                {
                    var manifest = ReadManifest(GetCurrentManifestPath());
                    targetFolder = manifest?.Folder;
                }

                if (string.IsNullOrEmpty(targetFolder) || !Directory.Exists(targetFolder))
                {
                    throw new InvalidOperationException("No valid runtime folder found");
                }

                UnloadCurrentRuntime();
                LoadRuntime(targetFolder);

                SendEvent("RELOAD_DONE", new { version = currentRuntime?.Version });
            }
            catch (Exception ex)
            {
                SendEvent("RELOAD_FAIL", new { error = ex.Message });
                throw;
            }
        }

        /// <summary>
        /// Shuts down the runtime manager.
        /// </summary>
        public void Shutdown()
        {
            UnloadCurrentRuntime();
        }

        private void LoadRuntime(string folder)
        {
            var assemblyPath = Path.Combine(folder, "Rca.Dynamic.dll");
            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException($"Runtime assembly not found: {assemblyPath}");
            }

            // Create new load context
            var contextName = $"RcaRuntime_{DateTime.Now:yyyyMMdd_HHmmss}";
            currentContext = new HotReloadAssemblyLoadContext(contextName);
            contextWeakRef = new WeakReference(currentContext);

            // Load assembly and find runtime implementation
            var assembly = currentContext.LoadAssembly(assemblyPath);
            var runtimeType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(IPluginRuntime).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            if (runtimeType == null)
            {
                throw new InvalidOperationException("No IPluginRuntime implementation found in assembly");
            }

            // Create and initialize runtime
            currentRuntime = (IPluginRuntime)Activator.CreateInstance(runtimeType);
            currentRuntime.Initialize(revitApplication);
            currentRuntime.OnLoaded();

            LogDebug($"Loaded runtime version {currentRuntime.Version} from {folder}");
        }

        private void UnloadCurrentRuntime()
        {
            if (currentRuntime != null)
            {
                try
                {
                    currentRuntime.Shutdown();
                }
                catch (Exception ex)
                {
                    LogDebug($"Error during runtime shutdown: {ex.Message}");
                }
                currentRuntime = null;
            }

            if (currentContext != null)
            {
                currentContext.Unload();
                currentContext = null;

                // Force garbage collection
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

#if DEBUG
                // Check if context was collected
                if (contextWeakRef?.IsAlive == false)
                {
                    SendEvent("ALC_COLLECTED", new { timestamp = DateTime.UtcNow });
                }
#endif
            }
        }

        private string GetCurrentManifestPath()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataPath, "RCA", "LiveCore", "current.json");
        }

        private RuntimeManifest ReadManifest(string path)
        {
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<RuntimeManifest>(json);
        }

        private void LogDebug(string message)
        {
            SendEvent("LOG", new { level = "Debug", message, source = "RuntimeManager" });
        }

        private void SendEvent(string eventName, object data)
        {
            // Event sending is handled by PipeServer if connected
            // For now, just log to console in debug builds
#if DEBUG
            Console.WriteLine($"[RuntimeManager] {eventName}: {JsonSerializer.Serialize(data)}");
#endif
        }

        private class RuntimeManifest
        {
            public string Folder { get; set; }
            public string Assembly { get; set; }
        }
    }
}