using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Rca.UI.Views;
using Rca.Core.Services;
using Rca.Core;
using Rca.Contracts;
using Rca.Contracts.Infrastructure;
using Rca.Runtime.Infrastructure;
using Rca.Loader.Contracts;
using System;
using System.Reflection;

namespace Rca.Runtime
{
    /// <summary>
    /// The runtime implementation that contains the actual RCA plugin logic.
    /// This class can be dynamically loaded and unloaded for hot reload functionality.
    /// </summary>
    public class RcaRuntime : IPluginRuntime
    {
        private const string DockablePaneGuid = "A1B2C3D4-E5F6-47A8-9B0C-1234567890AB";
        private const string DockablePaneName = "RCA Chat Assistant";
        private const string RibbonTabName = "RCA Plugin";
        private const string RibbonPanelName = "Chat Panel";
        private const string ButtonText = "Chat Assistant";

        private UIControlledApplication currentApplication;
        private bool isInitialized = false;

        /// <summary>
        /// Gets the version of the runtime.
        /// </summary>
        public string Version 
        { 
            get 
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                var commitHash = GetCommitHash();
                return $"{version}" + (string.IsNullOrEmpty(commitHash) ? "" : $"-{commitHash}");
            } 
        }

        /// <summary>
        /// Initializes the runtime with Revit application context.
        /// </summary>
        /// <param name="application">The Revit UI controlled application</param>
        public void Initialize(UIControlledApplication application)
        {
            if (isInitialized)
            {
                throw new InvalidOperationException("Runtime is already initialized");
            }

            try
            {
                currentApplication = application;

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
                    typeof(ShowDockablePanelCommand).FullName);
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
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize runtime: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Shuts down the runtime and cleans up resources.
        /// </summary>
        public void Shutdown()
        {
            if (!isInitialized)
                return;

            try
            {
                // Clean up any resources if needed
                // Note: Revit UI elements will be cleaned up when the assembly is unloaded
                isInitialized = false;
            }
            catch (Exception ex)
            {
                // Log but don't throw during shutdown
                System.Diagnostics.Debug.WriteLine($"Error during runtime shutdown: {ex.Message}");
            }
        }

        /// <summary>
        /// Called after the runtime has been loaded (optional hook).
        /// </summary>
        public void OnLoaded()
        {
            // Optional: Log loading event or perform post-load initialization
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"RCA Runtime loaded: {Version}");
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

        private string GetCommitHash()
        {
            try
            {
                // Try to get commit hash from assembly metadata
                var assembly = Assembly.GetExecutingAssembly();
                var attributes = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false);
                foreach (AssemblyMetadataAttribute attr in attributes)
                {
                    if (attr.Key == "GitCommitHash")
                    {
                        return attr.Value?.Substring(0, Math.Min(8, attr.Value.Length));
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
            return null;
        }
    }
}