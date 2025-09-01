using Autodesk.Revit.UI;
using Rca.Loader.Contracts;
using System;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Threading;
using System;
using Newtonsoft.Json;

namespace Rca.Loader
{
    /// <summary>
    /// Manages the lifecycle of hot-reloadable runtime assemblies.
    /// </summary>
    public class RuntimeManager
    {
        private readonly string stagingRoot;
        private HotReloadAssemblyLoadContext currentContext;
        private IPluginRuntime currentRuntime;
        private UIControlledApplication application;
        private WeakReference contextWeakRef;

        public RuntimeManager()
        {
            stagingRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RCA", "LiveCore");
            Directory.CreateDirectory(stagingRoot);
        }

        /// <summary>
        /// Loads the runtime assembly from the current manifest.
        /// </summary>
        public void LoadRuntime(UIControlledApplication app)
        {
            application = app;
            
            try
            {
                var manifest = ReadManifest();
                if (manifest != null)
                {
                    LoadRuntimeFromFolder(manifest.Folder, manifest.Assembly);
                }
                else
                {
                    // No manifest found - this is expected on first run
                    TaskDialog.Show("RCA Loader", "No runtime manifest found. Build Rca.Runtime project to create initial runtime.");
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Loader Error", $"Failed to load runtime: {ex.Message}");
            }
        }

        /// <summary>
        /// Reloads the runtime assembly.
        /// </summary>
        public void Reload(bool force = false)
        {
            try
            {
                var manifest = ReadManifest();
                if (manifest == null)
                {
                    throw new InvalidOperationException("No runtime manifest found");
                }

                // Unload current runtime
                UnloadCurrentRuntime();

                // Load new runtime
                LoadRuntimeFromFolder(manifest.Folder, manifest.Assembly);

#if DEBUG
                // Log successful reload for debugging
                Console.WriteLine($"[DEBUG] Runtime reloaded from {manifest.Folder}");
#endif
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Loader Error", $"Failed to reload runtime: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Unloads the current runtime.
        /// </summary>
        public void UnloadCurrentRuntime()
        {
            try
            {
                // Shutdown current runtime
                currentRuntime?.Shutdown();
                currentRuntime = null;

                // Unload assembly context
                if (currentContext != null)
                {
                    contextWeakRef = new WeakReference(currentContext);
                    currentContext.Unload();
                    currentContext = null;

                    // Force garbage collection to release the context
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

#if DEBUG
                    // Check if context was actually collected
                    if (!contextWeakRef.IsAlive)
                    {
                        Console.WriteLine("[DEBUG] AssemblyLoadContext successfully collected");
                    }
                    else
                    {
                        Console.WriteLine("[DEBUG] Warning: AssemblyLoadContext not yet collected");
                    }
#endif
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail on unload errors
                Console.WriteLine($"[WARNING] Error during runtime unload: {ex.Message}");
            }
        }

        private void LoadRuntimeFromFolder(string folder, string assemblyName)
        {
            var assemblyPath = Path.Combine(folder, assemblyName);
            
            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException($"Runtime assembly not found: {assemblyPath}");
            }

            // Create new assembly load context
            currentContext = new HotReloadAssemblyLoadContext(assemblyPath);
            
            // Load the assembly
            var assembly = currentContext.LoadAssembly();
            
            // Find IPluginRuntime implementation
            var runtimeType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(IPluginRuntime).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
            
            if (runtimeType == null)
            {
                throw new InvalidOperationException($"No IPluginRuntime implementation found in {assemblyName}");
            }

            // Create runtime instance
            currentRuntime = (IPluginRuntime)Activator.CreateInstance(runtimeType);
            
            // Initialize runtime
            currentRuntime.OnLoaded();
            currentRuntime.Initialize(application);
        }

        private RuntimeManifest ReadManifest()
        {
            var manifestPath = Path.Combine(stagingRoot, "current.json");
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            var json = File.ReadAllText(manifestPath);
            return JsonConvert.DeserializeObject<RuntimeManifest>(json);
        }

        private class RuntimeManifest
        {
            public string Folder { get; set; }
            public string Assembly { get; set; }
        }
    }
}