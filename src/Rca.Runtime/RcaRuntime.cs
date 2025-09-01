using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Rca.UI.Views;
using Rca.Core.Services;
using Rca.Core;
using Rca.Contracts;
using Rca.Contracts.Infrastructure;
using Rca.Loader.Contracts;
using System;
using System.Reflection;

namespace Rca.Runtime
{
    /// <summary>
    /// Hot-reloadable runtime implementation of the RCA Plugin.
    /// </summary>
    public class RcaRuntime : IPluginRuntime
    {
        private const string DockablePaneGuid = "A1B2C3D4-E5F6-47A8-9B0C-1234567890AB";
        private const string DockablePaneName = "RCA Chat Assistant";
        private const string RibbonTabName = "RCA Plugin";
        private const string RibbonPanelName = "Chat Panel";
        private const string ButtonText = "Chat Assistant";

        /// <summary>
        /// Gets the version of the runtime implementation.
        /// </summary>
        public string Version 
        { 
            get 
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                
                // Try to get commit hash from assembly metadata
                var commitHash = assembly.GetCustomAttribute<AssemblyMetadataAttribute>("CommitHash")?.Value;
                
                if (!string.IsNullOrEmpty(commitHash))
                {
                    return $"{version}+{commitHash[..8]}";
                }
                
                return version?.ToString() ?? "1.0.0.0";
            }
        }

        /// <summary>
        /// Initializes the runtime with the Revit UI application.
        /// </summary>
        public void Initialize(UIControlledApplication application)
        {
            try
            {
                // Setup dependency injection
                SetupServices();

                // Create ribbon tab and panel
                try { application.CreateRibbonTab(RibbonTabName); } catch { }
                var panel = application.CreateRibbonPanel(RibbonTabName, RibbonPanelName);

                // Create push button
                var buttonData = new PushButtonData(
                    "ShowChatPanel",
                    ButtonText,
                    Assembly.GetExecutingAssembly().Location,
                    typeof(Rca.Runtime.Commands.ShowDockablePanelCommand).FullName);
                panel.AddItem(buttonData);

                // Register dockable pane with dependency-injected provider
                var dpId = new DockablePaneId(new Guid(DockablePaneGuid));
                var container = ServiceContainer.Instance;
                var provider = new RcaDockablePanelProvider(
                    () => container.Resolve<IRevitContext>().CurrentUIApplication as UIApplication,
                    container.Resolve<IPythonExecutionService>(),
                    container.Resolve<IDebugLogService>());
                application.RegisterDockablePane(dpId, DockablePaneName, provider);

#if DEBUG
                Console.WriteLine($"[DEBUG] RCA Runtime {Version} initialized successfully");
#endif
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Runtime Error", $"Failed to initialize runtime: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Shuts down the runtime and cleans up resources.
        /// </summary>
        public void Shutdown()
        {
            try
            {
                // Clean up any runtime-specific resources
                // Note: Revit manages the UI elements, so we don't need to explicitly remove them
                
#if DEBUG
                Console.WriteLine($"[DEBUG] RCA Runtime {Version} shutdown completed");
#endif
            }
            catch (Exception ex)
            {
                // Log but don't fail on shutdown errors
                Console.WriteLine($"[WARNING] Error during runtime shutdown: {ex.Message}");
            }
        }

        /// <summary>
        /// Called after the runtime has been loaded into the new AssemblyLoadContext.
        /// </summary>
        public void OnLoaded()
        {
#if DEBUG
            Console.WriteLine($"[DEBUG] RCA Runtime {Version} loaded into AssemblyLoadContext");
#endif
        }

        /// <summary>
        /// Sets up the dependency injection container.
        /// </summary>
        private void SetupServices()
        {
            var container = ServiceContainer.Instance;

            // Register core services
            container.Register<IPythonExecutionService>(new PythonExecutionService());
            container.Register<IDebugLogService>(DebugLogService.Instance);
            container.Register<IRevitContext>(RevitContext.Instance);
        }
    }
}