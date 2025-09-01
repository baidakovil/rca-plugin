using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Rca.UI.Views;
using Rca.Core.Services;
using Rca.Core;
using Rca.Contracts;
using Rca.Contracts.Infrastructure;
using Rca.Loader.Contracts;
using Rca.Runtime.Commands;
using System;
using System.Reflection;

namespace Rca.Runtime
{
    /// <summary>
    /// The hot-reloadable runtime implementation of the RCA Plugin.
    /// This class contains the business logic that was previously in RcaPluginApp.
    /// </summary>
    public class RcaRuntime : IPluginRuntime
    {
        private const string DockablePaneGuid = "A1B2C3D4-E5F6-47A8-9B0C-1234567890AB";
        private const string DockablePaneName = "RCA Chat Assistant";
        private const string RibbonTabName = "RCA Plugin";
        private const string RibbonPanelName = "Chat Panel";
        private const string ButtonText = "Chat Assistant";

        private UIControlledApplication uiApplication;
        private bool isInitialized;

        /// <summary>
        /// Gets the version of the runtime.
        /// </summary>
        public string Version
        {
            get
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                
                // Return semantic version if available, otherwise assembly version
                if (informationalVersion != null && !string.IsNullOrEmpty(informationalVersion.InformationalVersion))
                {
                    return informationalVersion.InformationalVersion;
                }
                
                return version?.ToString() ?? "Unknown";
            }
        }

        /// <summary>
        /// Initializes the runtime with the provided UI application.
        /// </summary>
        /// <param name="application">The Revit UI application.</param>
        public void Initialize(UIControlledApplication application)
        {
            if (isInitialized)
            {
                throw new InvalidOperationException("Runtime is already initialized.");
            }

            try
            {
                uiApplication = application ?? throw new ArgumentNullException(nameof(application));

                // Setup dependency injection
                SetupServices();

                // Create ribbon tab and panel
                CreateRibbonUI(application);

                // Register dockable pane
                RegisterDockablePane(application);

                isInitialized = true;
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
                // Note: Ribbon tabs and dockable panes are managed by Revit and don't need explicit cleanup
                isInitialized = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during runtime shutdown: {ex.Message}");
            }
        }

        /// <summary>
        /// Called after the runtime has been loaded into the new assembly context.
        /// </summary>
        public void OnLoaded()
        {
            System.Diagnostics.Debug.WriteLine($"RCA Runtime loaded, version: {Version}");
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
        /// Creates the ribbon UI components.
        /// </summary>
        /// <param name="application">The UI application.</param>
        private void CreateRibbonUI(UIControlledApplication application)
        {
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
        }

        /// <summary>
        /// Registers the dockable pane.
        /// </summary>
        /// <param name="application">The UI application.</param>
        private void RegisterDockablePane(UIControlledApplication application)
        {
            var dpId = new DockablePaneId(new Guid(DockablePaneGuid));
            var container = ServiceContainer.Instance;
            var provider = new RcaDockablePanelProvider(
                () => container.Resolve<IRevitContext>().CurrentUIApplication as UIApplication,
                container.Resolve<IPythonExecutionService>(),
                container.Resolve<IDebugLogService>());
            application.RegisterDockablePane(dpId, DockablePaneName, provider);
        }
    }
}