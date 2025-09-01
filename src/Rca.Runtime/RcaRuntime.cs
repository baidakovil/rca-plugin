using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Rca.Contracts;
using Rca.Contracts.Infrastructure;
using Rca.Core;
using Rca.Core.Services;
using Rca.Loader.Contracts;
using Rca.UI.Views;
using System;
using System.Reflection;

namespace Rca.Runtime
{
    /// <summary>
    /// The hot-reloadable runtime implementation for the RCA Plugin.
    /// This class contains all the logic previously in RcaPluginApp.
    /// </summary>
    public class RcaRuntime : IPluginRuntime
    {
        private const string DockablePaneGuid = "A1B2C3D4-E5F6-47A8-9B0C-1234567890AB";
        private const string DockablePaneName = "RCA Chat Assistant";
        private const string RibbonTabName = "RCA Plugin";
        private const string RibbonPanelName = "Chat Panel";
        private const string ButtonText = "Chat Assistant";

        /// <summary>
        /// Gets the version of the runtime.
        /// </summary>
        public string Version 
        { 
            get 
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                
                // Try to get commit hash or build info if available
                var commitHash = GetCommitHash();
                if (!string.IsNullOrEmpty(commitHash))
                {
                    return $"{version}-{commitHash}";
                }
                
                return version?.ToString() ?? "1.0.0.0";
            } 
        }

        /// <summary>
        /// Initializes the runtime with the Revit application.
        /// </summary>
        /// <param name="application">The Revit UI application</param>
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

                LogMessage($"RCA Runtime {Version} initialized successfully");
            }
            catch (Exception ex)
            {
                LogMessage($"Error initializing runtime: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Shuts down the runtime.
        /// </summary>
        public void Shutdown()
        {
            try
            {
                LogMessage($"RCA Runtime {Version} shutting down");
                // Cleanup logic if needed
            }
            catch (Exception ex)
            {
                LogMessage($"Error during runtime shutdown: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Called when the runtime is loaded.
        /// </summary>
        public void OnLoaded()
        {
            LogMessage($"RCA Runtime {Version} loaded and ready");
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
        /// Gets the commit hash if available.
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
                    {
                        return attr.Value;
                    }
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Logs a message.
        /// </summary>
        private void LogMessage(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[RcaRuntime] {message}");
            
            // Also log to the debug service if available
            try
            {
                var container = ServiceContainer.Instance;
                var debugService = container.Resolve<IDebugLogService>();
                debugService?.LogInfo($"Runtime: {message}");
            }
            catch
            {
                // Ignore errors when logging to debug service
            }
        }
    }
}