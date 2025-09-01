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
    /// This class contains the main plugin logic that can be unloaded and reloaded.
    /// </summary>
    public class RcaRuntime : IPluginRuntime
    {
        private const string DockablePaneGuid = "A1B2C3D4-E5F6-47A8-9B0C-1234567890AB";
        private const string DockablePaneName = "RCA Chat Assistant";
        private const string RibbonTabName = "RCA Plugin";
        private const string RibbonPanelName = "Chat Panel";
        private const string ButtonText = "Chat Assistant";

        private bool isInitialized = false;

        /// <summary>
        /// Gets the version of the runtime implementation.
        /// </summary>
        public string Version 
        { 
            get 
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                var commitHash = GetCommitHash();
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                
                return $"{version}.{timestamp}" + (string.IsNullOrEmpty(commitHash) ? "" : $"+{commitHash}");
            } 
        }

        /// <summary>
        /// Called after the runtime has been loaded into the new AssemblyLoadContext.
        /// </summary>
        public void OnLoaded()
        {
            // Perform any post-load initialization here
            System.Diagnostics.Debug.WriteLine($"[RCA Runtime] OnLoaded called for version {Version}");
        }

        /// <summary>
        /// Initializes the plugin runtime with the Revit UI application.
        /// </summary>
        /// <param name="application">The UIControlledApplication from Revit</param>
        public void Initialize(UIControlledApplication application)
        {
            try
            {
                if (isInitialized)
                    return;

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
                    typeof(Commands.ShowDockablePanelCommand).FullName);
                panel.AddItem(buttonData);

                // Register dockable pane with dependency-injected provider
                var dpId = new DockablePaneId(new Guid(DockablePaneGuid));
                var container = ServiceContainer.Instance;
                var provider = new RcaDockablePanelProvider(
                    () => container.Resolve<IRevitContext>().CurrentUIApplication as UIApplication,
                    container.Resolve<IPythonExecutionService>(),
                    container.Resolve<IDebugLogService>());
                application.RegisterDockablePane(dpId, DockablePaneName, provider);

                isInitialized = true;
                System.Diagnostics.Debug.WriteLine($"[RCA Runtime] Initialized successfully: {Version}");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Runtime Error", $"Failed to initialize runtime: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Shuts down the plugin runtime and cleans up resources.
        /// </summary>
        public void Shutdown()
        {
            try
            {
                // Clean up any resources here
                // Note: Revit panels and ribbons are managed by Revit itself
                isInitialized = false;
                System.Diagnostics.Debug.WriteLine($"[RCA Runtime] Shutdown completed: {Version}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RCA Runtime] Error during shutdown: {ex.Message}");
                // Don't throw exceptions during shutdown
            }
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

        /// <summary>
        /// Gets the commit hash from assembly metadata if available.
        /// </summary>
        private string GetCommitHash()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var attributes = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false);
                foreach (AssemblyMetadataAttribute attr in attributes)
                {
                    if (attr.Key == "CommitHash")
                        return attr.Value?.Substring(0, Math.Min(8, attr.Value.Length));
                }
            }
            catch
            {
                // Ignore errors getting commit hash
            }
            return "";
        }
    }
}