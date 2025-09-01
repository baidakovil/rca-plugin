using Autodesk.Revit.UI;
using Rca.Loader.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace Rca.Loader
{
    /// <summary>
    /// Manages the runtime assembly lifecycle for hot reloading.
    /// </summary>
    internal class RuntimeManager
    {
        private const string ManifestFileName = "current.json";
        private const string RuntimeAssemblyName = "Rca.Dynamic.dll";
        private readonly string stagingRootPath;
        private HotReloadAssemblyLoadContext currentContext;
        private IPluginRuntime currentRuntime;
        private UIControlledApplication uiApplication;
        private readonly List<WeakReference> previousContexts = new List<WeakReference>();

        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeManager"/> class.
        /// </summary>
        public RuntimeManager()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            stagingRootPath = Path.Combine(localAppData, "RCA", "LiveCore");
        }

        /// <summary>
        /// Initializes the runtime manager with the UI application.
        /// </summary>
        /// <param name="application">The UI application.</param>
        public void Initialize(UIControlledApplication application)
        {
            uiApplication = application;
            
            // Try to load the initial runtime
            try
            {
                LoadRuntimeFromManifest();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Loader", $"Failed to load initial runtime: {ex.Message}");
            }
        }

        /// <summary>
        /// Reloads the runtime assembly.
        /// </summary>
        /// <param name="folderOverride">Optional folder override for the runtime assembly.</param>
        /// <param name="force">Whether to force reload even if no manifest changes.</param>
        public void Reload(string folderOverride = null, bool force = false)
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
                        // Log but don't fail the reload
                        System.Diagnostics.Debug.WriteLine($"Error shutting down current runtime: {ex.Message}");
                    }
                }

                // Unload current context
                if (currentContext != null)
                {
                    previousContexts.Add(currentContext.GetWeakReference());
                    currentContext.Unload();
                    currentContext = null;
                    currentRuntime = null;

                    // Trigger garbage collection to help unload
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

#if DEBUG
                    // Check if previous contexts are collected
                    CheckPreviousContextsCollection();
#endif
                }

                // Load new runtime
                if (!string.IsNullOrEmpty(folderOverride))
                {
                    LoadRuntimeFromFolder(folderOverride);
                }
                else
                {
                    LoadRuntimeFromManifest();
                }

                System.Diagnostics.Debug.WriteLine($"Runtime reloaded successfully. Version: {currentRuntime?.Version}");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Loader", $"Failed to reload runtime: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Shuts down the runtime manager.
        /// </summary>
        public void Shutdown()
        {
            try
            {
                currentRuntime?.Shutdown();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error shutting down runtime: {ex.Message}");
            }

            try
            {
                currentContext?.Unload();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error unloading context: {ex.Message}");
            }
        }

        private void LoadRuntimeFromManifest()
        {
            var manifestPath = Path.Combine(stagingRootPath, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException($"Runtime manifest not found at: {manifestPath}");
            }

            var manifestJson = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<RuntimeManifest>(manifestJson);
            
            if (string.IsNullOrEmpty(manifest.Folder))
            {
                throw new InvalidOperationException("Runtime manifest does not specify a folder.");
            }

            var runtimeFolder = Path.IsPathRooted(manifest.Folder) 
                ? manifest.Folder 
                : Path.Combine(stagingRootPath, manifest.Folder);

            LoadRuntimeFromFolder(runtimeFolder);
        }

        private void LoadRuntimeFromFolder(string folder)
        {
            var assemblyPath = Path.Combine(folder, RuntimeAssemblyName);
            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException($"Runtime assembly not found at: {assemblyPath}");
            }

            // Create new assembly load context
            var contextName = $"Runtime_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
            currentContext = new HotReloadAssemblyLoadContext(contextName);

            // Load the runtime assembly
            var assembly = currentContext.LoadFromAssemblyPath(assemblyPath);

            // Find and instantiate the runtime
            var runtimeType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(IPluginRuntime).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            if (runtimeType == null)
            {
                throw new InvalidOperationException($"No implementation of {nameof(IPluginRuntime)} found in assembly: {assemblyPath}");
            }

            currentRuntime = (IPluginRuntime)Activator.CreateInstance(runtimeType);
            
            // Initialize the runtime
            if (uiApplication != null)
            {
                currentRuntime.Initialize(uiApplication);
            }

            // Notify that runtime is loaded
            currentRuntime.OnLoaded();
        }

#if DEBUG
        private void CheckPreviousContextsCollection()
        {
            var collected = 0;
            var alive = 0;

            for (int i = previousContexts.Count - 1; i >= 0; i--)
            {
                var weakRef = previousContexts[i];
                if (weakRef.IsAlive)
                {
                    alive++;
                }
                else
                {
                    collected++;
                    previousContexts.RemoveAt(i);
                }
            }

            if (collected > 0)
            {
                System.Diagnostics.Debug.WriteLine($"ALC_COLLECTED: {collected} contexts collected, {alive} still alive");
            }
        }
#endif

        /// <summary>
        /// Represents the runtime manifest structure.
        /// </summary>
        private class RuntimeManifest
        {
            public string Folder { get; set; }
            public string Assembly { get; set; }
        }
    }
}