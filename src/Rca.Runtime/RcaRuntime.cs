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
    /// The hot-reloadable runtime implementation for the RCA Plugin.
    /// Contains all the business logic extracted from the original RcaPluginApp.
    /// </summary>
    public class RcaRuntime : IPluginRuntime
    {
        private const string DockablePaneGuid = "A1B2C3D4-E5F6-47A8-9B0C-1234567890AB";
        private const string DockablePaneName = "RCA Chat Assistant";
        private const string RibbonTabName = "RCA Plugin";
        private const string RibbonPanelName = "Chat Panel";
        private const string ButtonText = "Chat Assistant";

        private UIControlledApplication uiApplication;

        /// <summary>
        /// Gets the runtime version including assembly version and optional commit hash.
        /// </summary>
        public string Version
        {
            get
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version?.ToString() ?? "1.0.0.0";
                
                // Try to get commit hash from assembly metadata
                var commitHash = assembly.GetCustomAttribute<AssemblyMetadataAttribute>()?.Value;
                if (!string.IsNullOrEmpty(commitHash))
                {
                    return $"{version}+{commitHash[..8]}"; // Use first 8 chars of commit hash
                }
                
                return version;
            }
        }

        /// <summary>
        /// Initializes the runtime with the Revit UI application.
        /// Sets up dependency injection, creates UI elements, and registers services.
        /// </summary>
        public void Initialize(UIControlledApplication application)
        {
            try
            {
                uiApplication = application;

                // Setup dependency injection
                SetupServices();

                // Create ribbon tab and panel
                CreateRibbonInterface(application);

                // Register dockable pane
                RegisterDockablePane(application);
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
                // Clean up any resources if needed
                // The service container will be cleaned up when the AssemblyLoadContext is unloaded
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Runtime Error", $"Error during shutdown: {ex.Message}");
            }
        }

        /// <summary>
        /// Called after the runtime has been loaded.
        /// </summary>
        public void OnLoaded()
        {
            // Optional hook for post-load initialization
        }

        /// <summary>
        /// Sets up the dependency injection container with core services.
        /// </summary>
        private void SetupServices()
        {
            var container = ServiceContainer.Instance;

            // Register core services
            container.Register<IPythonExecutionService>(new PythonExecutionService());
            container.Register<IDebugLogService>(DebugLogService.Instance);
            container.Register<IRevitContext>(RevitContext.Instance);
        }

        /// <summary>
        /// Creates the ribbon interface for the RCA Plugin.
        /// </summary>
        private void CreateRibbonInterface(UIControlledApplication application)
        {
            try
            {
                // Create ribbon tab and panel
                try { application.CreateRibbonTab(RibbonTabName); } catch { }
                var panel = application.CreateRibbonPanel(RibbonTabName, RibbonPanelName);

                // Create push button
                var buttonData = new PushButtonData(
                    "ShowChatPanel",
                    ButtonText,
                    Assembly.GetExecutingAssembly().Location,
                    typeof(Commands.ShowDockablePanelCommand).FullName);
                
                panel.AddItem(buttonData);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Runtime Warning", $"Failed to create ribbon interface: {ex.Message}");
            }
        }

        /// <summary>
        /// Registers the dockable pane with dependency-injected services.
        /// </summary>
        private void RegisterDockablePane(UIControlledApplication application)
        {
            try
            {
                var dpId = new DockablePaneId(new Guid(DockablePaneGuid));
                var container = ServiceContainer.Instance;
                var provider = new RcaDockablePanelProvider(
                    () => container.Resolve<IRevitContext>().CurrentUIApplication as UIApplication,
                    container.Resolve<IPythonExecutionService>(),
                    container.Resolve<IDebugLogService>());
                
                application.RegisterDockablePane(dpId, DockablePaneName, provider);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Runtime Warning", $"Failed to register dockable pane: {ex.Message}");
            }
        }
    }
}