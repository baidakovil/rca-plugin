using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Rca.Contracts.Infrastructure;
using Rca.Loader.Services;
using System;

namespace Rca.Loader
{
    /// <summary>
    /// The main loader application class for the RCA Plugin.
    /// This minimal loader handles hot-reloading of the main plugin assembly.
    /// </summary>
    public class RcaLoaderApp : IExternalApplication
    {
        private const string PipeName = "RcaPluginReloader";
        private const string DefaultPluginPath = @"RcaPlugin\RcaPlugin.dll";
        
        private IPluginLoader pluginLoader;
        private INamedPipeService namedPipeService;

        /// <summary>
        /// Called when Revit starts up.
        /// </summary>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Initialize services
                pluginLoader = new PluginLoaderService();
                namedPipeService = new NamedPipeService();

                // Setup event handlers
                pluginLoader.LoadingFailed += OnPluginLoadingFailed;
                namedPipeService.ReloadRequested += OnReloadRequested;

                // Start named pipe server for reload commands
                namedPipeService.StartServer(PipeName);

                // Load the main plugin
                var pluginPath = GetPluginPath();
                if (!pluginLoader.LoadPlugin(pluginPath))
                {
                    TaskDialog.Show("RCA Loader", $"Failed to load plugin from: {pluginPath}");
                    return Result.Failed;
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Loader Error", ex.Message);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Called when Revit shuts down.
        /// </summary>
        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                // Cleanup
                namedPipeService?.StopServer();
                pluginLoader?.UnloadPlugin();
                
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Loader Shutdown Error", ex.Message);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Gets the path to the main plugin assembly.
        /// </summary>
        private string GetPluginPath()
        {
            var loaderPath = typeof(RcaLoaderApp).Assembly.Location;
            var pluginDir = System.IO.Path.GetDirectoryName(loaderPath);
            return System.IO.Path.Combine(pluginDir, DefaultPluginPath);
        }

        /// <summary>
        /// Handles plugin loading failures.
        /// </summary>
        private void OnPluginLoadingFailed(object sender, string error)
        {
            TaskDialog.Show("RCA Plugin Loading Failed", error);
        }

        /// <summary>
        /// Handles reload requests from named pipe.
        /// </summary>
        private void OnReloadRequested(object sender, string assemblyPath)
        {
            try
            {
                var pathToReload = string.IsNullOrEmpty(assemblyPath) ? GetPluginPath() : assemblyPath;
                
                if (pluginLoader.ReloadPlugin(pathToReload))
                {
                    // Optionally show success message
                }
                else
                {
                    TaskDialog.Show("RCA Plugin Reload", $"Failed to reload plugin from: {pathToReload}");
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Plugin Reload Error", ex.Message);
            }
        }
    }
}