using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Rca.Loader.Contracts;
using Rca.Loader.Services;
using Rca.Loader.Infrastructure;
using Rca.Loader.AssemblyManagement;
using Rca.Loader.UI;
using Rca.Loader.Logging;
using Rca.Loader.Configuration;
using Microsoft.Extensions.Logging;

namespace Rca.Loader
{
    /// <summary>
    /// Main entry point for the RCA Loader Revit add-in.
    /// Automatically initializes pipe server when UIApplication becomes available!
    /// Optionally auto-loads runtime based on settings.
    /// </summary>
    public class LoaderApp : IExternalApplication
    {
        private IPipeServerService? pipeServer;
        private IRibbonService ribbonService;
        private RuntimeCommandHandler? commandHandler;
        private UIApplication? uiapp;
        private AssemblyStatusManager? assemblyStatusManager;
        private UIControlledApplication? uiControlledApp;
        private LoggingPipeServerService? loggingPipe; // logging server
        private ILogger _log = LoaderLog.GetLogger<LoaderApp>();
        private bool isInitialized = false; // Track initialization state
        private Settings settings; // Application settings

        /// <summary>
        /// The dockable pane id used to register the RCA panel.
        /// Keep this GUID stable across builds so user layout persists.
        /// </summary>
        private static readonly DockablePaneId DockablePaneId = new DockablePaneId(new Guid("3D5A1C2B-4F8E-4D3F-AF1E-1234567890AB"));

        /// <summary>
        /// Gets the runtime manager instance.
        /// </summary>
        public IRuntimeManager RuntimeManager { get; }

        /// <summary>
        /// Gets the assembly status manager instance.
        /// </summary>
        public AssemblyStatusManager? AssemblyStatusManager => assemblyStatusManager;

        /// <summary>
        /// Gets the singleton instance of the loader application.
        /// Exposed for integration testing.
        /// </summary>
        public static LoaderApp? Instance { get; private set; }

        /// <summary>
        /// Gets the Revit UI application.
        /// </summary>
        public UIApplication? UIApplication => uiapp;

        /// <summary>
        /// Host instance registered in the dockable pane. Can be null until pane is registered.
        /// </summary>
        public Rca.Loader.Contracts.IRuntimePanelHost? PanelHost { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoaderApp"/> class.
        /// </summary>
        public LoaderApp()
        {
            Instance = this;
            RuntimeManager = new RuntimeManager();
            ribbonService = new RibbonService();
            
            // Load settings early
            settings = SettingsService.LoadSettings();
        }

        /// <summary>
        /// Called when Revit starts up.
        /// </summary>
        /// <param name="application">The Revit UI application.</param>
        /// <returns>Result of the operation.</returns>
        public Result OnStartup(UIControlledApplication application)
        {
            if (application is null)
                throw new ArgumentNullException(nameof(application));

            try
            {
                _log.LogInformation("Loader startup begin");
                this.uiControlledApp = application;

                // Initialize assembly status manager
                assemblyStatusManager = new AssemblyStatusManager();
                assemblyStatusManager.InitializeOnStartup();

                // Start logging pipe server early (must exist before runtime logger tries to connect)
                loggingPipe = new LoggingPipeServerService("RCA_LOG_PIPE");
                loggingPipe.Start();

                // Build the ribbon UI
                ribbonService.BuildRibbon(application);

                // Register minimal dockable pane host so Revit shows placeholder UI without loading runtime
                try
                {
                    var host = new DockablePanelHost();
                    var provider = new DockablePanelProvider(host);

                    application.RegisterDockablePane(DockablePaneId, "RCA Chat Assistant", provider);

                    // Store reference to the host as contract interface for later swapping
                    PanelHost = host;

                    // Do NOT try to show pane here - Revit hasn't finished registration yet
                    // Pane will be shown automatically when user clicks on it or via command
                    _log.LogInformation("Dockable pane registered successfully id={Id}", DockablePaneId.Guid);
                }
                catch (Exception exReg)
                {
                    _log.LogError(exReg, "Failed to register dockable pane");
                }

                // Hook into application events for auto-initialization
                application.Idling += OnApplicationIdling;

                // Update status display if available
    #if DEBUG
                var statusDisplay = ((RibbonService)ribbonService).StatusDisplay;
                if (statusDisplay != null && assemblyStatusManager != null)
                {
                    _log.LogDebug("Updating status display with initial values: loaderHash={LoaderHash} runtimeHash={RuntimeHash}", assemblyStatusManager.CurrentInfo.LoaderComponents.Hash, assemblyStatusManager.CurrentInfo.RuntimeAssembly.Hash);
                    statusDisplay.UpdateStatus(assemblyStatusManager.CurrentInfo);
                }
    #endif
                _log.LogInformation("Loader startup completed successfully");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                _log.LogCritical(ex, "Loader startup failed");
                TaskDialog.Show("RCA Loader Error", ex.ToString());
                return Result.Failed;
            }
        }

        /// <summary>
        /// Called when Revit shuts down.
        /// </summary>
        /// <param name="application">The Revit UI application.</param>
        /// <returns>Result of the operation.</returns>
        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                _log.LogInformation("Loader shutdown begin");

                // Unsubscribe from events
                if (uiControlledApp != null)
                {
                    uiControlledApp.Idling -= OnApplicationIdling;
                }

                pipeServer?.Stop();
                loggingPipe?.Dispose();
                RuntimeManager.UnloadRuntime();
                _log.LogInformation("Loader shutdown completed");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error during loader shutdown");
            }
            return Result.Succeeded;
        }

        /// <summary>
        /// Handles the Idling event to auto-initialize pipe server when UIApplication becomes available.
        /// Also monitors pipe server health and restarts if needed.
        /// Optionally auto-loads runtime based on settings.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnApplicationIdling(object? sender, IdlingEventArgs e)
        {
            // First-time initialization when UIApplication becomes available
            if (!isInitialized && uiapp == null && sender is UIApplication uiApplication)
            {
                _log.LogDebug("Auto-initializing pipe server on first Idling event");
                InitializeWithUIApplication(uiApplication);
                isInitialized = true;
                
                // Auto-load runtime if enabled in settings
                if (settings.AutoLoadRuntimeOnStartup)
                {
                    _log.LogInformation("Auto-loading runtime (AutoLoadRuntimeOnStartup=true)");
                    AutoLoadRuntime();
                }
                else
                {
                    _log.LogInformation("Skipping auto-load runtime (AutoLoadRuntimeOnStartup=false)");
                }
                
                return;
            }

            // Health check: restart pipe server if it stopped unexpectedly
            if (isInitialized && uiapp != null && pipeServer != null && !pipeServer.IsRunning)
            {
                _log.LogWarning("Pipe server stopped unexpectedly, restarting...");
                try
                {
                    StartPipeServer();
                    _log.LogInformation("Pipe server restarted successfully");
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to restart pipe server");
                }
            }
        }

        /// <summary>
        /// Automatically loads the latest runtime on startup.
        /// Called during first Idling event if AutoLoadRuntimeOnStartup setting is true.
        /// </summary>
        private void AutoLoadRuntime()
        {
            try
            {
                if (RuntimeManager.IsRuntimeLoaded)
                {
                    _log.LogDebug("Runtime already loaded, skipping auto-load");
                    return;
                }

                _log.LogInformation("Auto-loading latest runtime");
                var success = RuntimeManager.ReloadLatest(out var error);
                
                if (success)
                {
                    // Update runtime hash after successful reload
                    if (assemblyStatusManager != null)
                    {
                        assemblyStatusManager.UpdateHashesAfterReload(RuntimeManager.CurrentRuntimePath);
                        UpdateStatusDisplay();
                    }

                    // Inject runtime UI into dockable panel host
                    TryInjectRuntimeUI();
                    
                    _log.LogInformation("Runtime auto-loaded successfully from {Path}", RuntimeManager.CurrentRuntimePath);

#if DEBUG
                    // Auto-show panel if enabled in debug settings
                    if (settings.Debug.AutoShowPanelOnLoad && uiapp != null)
                    {
                        _log.LogDebug("Auto-showing dockable panel (Debug.AutoShowPanelOnLoad=true)");
                        TryShowDockablePanel();
                    }
#endif
                }
                else
                {
                    _log.LogWarning("Auto-load runtime failed: {Error}", error);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Exception during auto-load runtime");
            }
        }

        /// <summary>
        /// Attempts to inject runtime UI into the dockable panel host after successful runtime load.
        /// </summary>
        private void TryInjectRuntimeUI()
        {
            try
            {
                var host = PanelHost;
                if (host == null)
                {
                    _log.LogWarning("PanelHost unavailable for UI injection");
                    return;
                }

                var content = RuntimeManager.CreateRuntimeDockableContent(out var error);
                if (content != null)
                {
                    host.SetContent(content);
                    _log.LogInformation("Runtime UI injected successfully");
                }
                else
                {
                    _log.LogWarning("Failed to create runtime UI: {Error}", error);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error injecting runtime UI");
            }
        }

#if DEBUG
        /// <summary>
        /// Attempts to show the dockable panel.
        /// Only available in DEBUG builds.
        /// </summary>
        private void TryShowDockablePanel()
        {
            try
            {
                if (uiapp == null)
                {
                    _log.LogDebug("UIApplication not available, cannot show dockable pane");
                    return;
                }

                var pane = uiapp.GetDockablePane(DockablePaneId);
                if (pane != null && !pane.IsShown())
                {
                    pane.Show();
                    _log.LogDebug("Dockable pane auto-shown");
                }
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "Error auto-showing dockable pane");
            }
        }
#endif

        /// <summary>
        /// Initializes the UIApplication and starts the pipe server.
        /// This method is called automatically on first Idling event.
        /// </summary>
        /// <param name="uiapp">The Revit UI application.</param>
        public void InitializeWithUIApplication(UIApplication uiapp)
        {
            if (this.uiapp == null && pipeServer == null)
            {
                this.uiapp = uiapp;
                StartPipeServer();
                _log.LogInformation("Pipe server auto-initialized successfully");
            }
        }

        /// <summary>
        /// Updates the status display with the current assembly information.
        /// </summary>
        public void UpdateStatusDisplay()
        {
    #if DEBUG
            try
            {
                var statusDisplay = ((RibbonService)ribbonService).StatusDisplay;
                if (statusDisplay != null && assemblyStatusManager != null)
                {
                    _log.LogDebug("UpdateStatusDisplay loaderHash={LoaderHash} runtimeHash={RuntimeHash} msbuild={Signal}", assemblyStatusManager.CurrentInfo.LoaderComponents.Hash, assemblyStatusManager.CurrentInfo.RuntimeAssembly.Hash, $"{assemblyStatusManager.CurrentInfo.LastMSBuildSignal.Time}-{assemblyStatusManager.CurrentInfo.LastMSBuildSignal.Event}");
                    statusDisplay.UpdateStatus(assemblyStatusManager.CurrentInfo);
                }
                else
                {
                    _log.LogDebug("UpdateStatusDisplay skipped: null dependencies");
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Error updating status display");
            }
    #endif
        }

        private void StartPipeServer()
        {
            if (uiapp == null) throw new InvalidOperationException("UIApplication not initialized");
            _log.LogInformation("Starting command pipe server");
            commandHandler = new RuntimeCommandHandler(RuntimeManager, uiapp);
            pipeServer = new PipeServerService(LoaderConstants.CommandPipeName, commandHandler.HandlePipeCommandAsync);
            pipeServer.Start();
            _log.LogInformation("Command pipe server started");
        }
    }
}
