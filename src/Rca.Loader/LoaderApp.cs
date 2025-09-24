using System;
using System.Diagnostics;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Events;
using Rca.Loader.Contracts;
using Rca.Loader.Services;
using Rca.Loader.Infrastructure;
using Rca.Loader.AssemblyManagement;

namespace Rca.Loader
{
    /// <summary>
    /// Main entry point for the RCA Loader Revit add-in.
    /// </summary>
    public class LoaderApp : IExternalApplication
    {
        private IPipeServerService? pipeServer;
        private IRibbonService ribbonService;
        private RuntimeCommandHandler? commandHandler;
        private UIApplication? uiapp;
        private AssemblyStatusManager? assemblyStatusManager;
        private UIControlledApplication? uiControlledApp;

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
        /// </summary>
        internal static LoaderApp? Instance { get; private set; }

        /// <summary>
        /// Gets the Revit UI application.
        /// </summary>
        public UIApplication? UIApplication => uiapp;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoaderApp"/> class.
        /// </summary>
        public LoaderApp()
        {
            Instance = this;
            RuntimeManager = new RuntimeManager();
            ribbonService = new RibbonService();
        }

        /// <summary>
        /// Called when Revit starts up.
        /// </summary>
        /// <param name="application">The Revit UI application.</param>
        /// <returns>Result of the operation.</returns>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                Debug.WriteLine("RCA Loader starting up");
                
                this.uiControlledApp = application;
                
                // Initialize assembly status manager
                assemblyStatusManager = new AssemblyStatusManager();
                assemblyStatusManager.InitializeOnStartup();
                
                // Build the ribbon UI
                ribbonService.BuildRibbon(application);
                
                // Hook into application events for auto-initialization
                application.Idling += OnApplicationIdling;
                
                // Update status display if available
#if DEBUG
                var statusDisplay = ((RibbonService)ribbonService).StatusDisplay;
                if (statusDisplay != null && assemblyStatusManager != null)
                {
                    Debug.WriteLine("Updating status display with initial values");
                    statusDisplay.UpdateStatus(assemblyStatusManager.CurrentInfo);
                }
#endif
                
                Debug.WriteLine("RCA Loader startup completed");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during RCA Loader startup: {ex.Message}\n{ex.StackTrace}");
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
                Debug.WriteLine("RCA Loader shutting down");
                
                // Unsubscribe from events
                if (uiControlledApp != null)
                {
                    uiControlledApp.Idling -= OnApplicationIdling;
                }
                
                pipeServer?.Stop();
                RuntimeManager.UnloadRuntime();
                Debug.WriteLine("RCA Loader shutdown completed");
            }
            catch (Exception ex) 
            {
                Debug.WriteLine($"Error during RCA Loader shutdown: {ex.Message}");
            }
            return Result.Succeeded;
        }

        /// <summary>
        /// Handles the Idling event to auto-initialize pipe server when UIApplication becomes available.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnApplicationIdling(object? sender, IdlingEventArgs e)
        {
            if (uiapp == null && sender is UIApplication uiApplication)
            {
                Debug.WriteLine("Auto-initializing pipe server via Idling event");
                InitializeWithUIApplication(uiApplication);
                
                // Unsubscribe after successful initialization
                if (uiControlledApp != null)
                {
                    uiControlledApp.Idling -= OnApplicationIdling;
                }
            }
        }

        /// <summary>
        /// Initializes the UIApplication and starts the pipe server.
        /// </summary>
        /// <param name="uiapp">The Revit UI application.</param>
        public void InitializeWithUIApplication(UIApplication uiapp)
        {
            if (this.uiapp == null && pipeServer == null)
            {
                this.uiapp = uiapp;
                StartPipeServer();
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
                    Debug.WriteLine("Updating status display");
                    statusDisplay.UpdateStatus(assemblyStatusManager.CurrentInfo);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating status display: {ex.Message}");
            }
#endif
        }

        private void StartPipeServer()
        {
            if (uiapp == null)
            {
                throw new InvalidOperationException("UIApplication not initialized");
            }
            
            Debug.WriteLine("Starting pipe server");
            commandHandler = new RuntimeCommandHandler(RuntimeManager, uiapp);
            pipeServer = new PipeServerService(LoaderConstants.PipeName, commandHandler.HandlePipeCommandAsync);
            pipeServer.Start();
            Debug.WriteLine("Pipe server started");
        }
    }
}
