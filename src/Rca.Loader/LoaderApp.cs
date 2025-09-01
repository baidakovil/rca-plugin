using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System;
using System.Reflection;

namespace Rca.Loader
{
    /// <summary>
    /// Main entry point for the RCA Loader - a stable shim that loads and manages the hot-reloadable runtime.
    /// </summary>
    public class LoaderApp : IExternalApplication
    {
        private RuntimeHost runtimeHost;
        private HotReloadServer hotReloadServer;

        /// <summary>
        /// Called when Revit starts up.
        /// </summary>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                Console.WriteLine("[HotReload] Loader starting up...");
                
                // Initialize runtime host
                runtimeHost = new RuntimeHost();
                
                // Load initial runtime
                if (!runtimeHost.LoadRuntime())
                {
                    Console.WriteLine("[HotReload] Failed to load initial runtime");
                    return Result.Failed;
                }

                // Start runtime
                if (!runtimeHost.StartupRuntime(application))
                {
                    Console.WriteLine("[HotReload] Failed to startup runtime");
                    return Result.Failed;
                }

                // Start hot-reload server
                hotReloadServer = new HotReloadServer(runtimeHost);
                hotReloadServer.Start();

                Console.WriteLine("[HotReload] Loader startup completed successfully");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HotReload] Loader startup failed: {ex.Message}");
                TaskDialog.Show("RCA Loader Error", $"Failed to start RCA Loader: {ex.Message}");
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
                Console.WriteLine("[HotReload] Loader shutting down...");
                
                hotReloadServer?.Stop();
                runtimeHost?.UnloadRuntime();
                
                Console.WriteLine("[HotReload] Loader shutdown completed");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HotReload] Loader shutdown error: {ex.Message}");
                return Result.Failed;
            }
        }
    }
}