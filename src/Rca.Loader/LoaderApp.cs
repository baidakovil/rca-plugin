using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Rca.Loader.Contracts;

namespace Rca.Loader
{
    /// <summary>
    /// Main entry point for the RCA Loader Revit add-in.
    /// </summary>
    public class LoaderApp : IExternalApplication
    {
        private PipeServer? pipeServer;
        private RibbonBuilder ribbonBuilder;
        private RuntimeCommandHandler? commandHandler;
        private UIApplication? uiapp;

        /// <summary>
        /// Gets the runtime manager instance.
        /// </summary>
        public RuntimeManager RuntimeManager { get; }

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
            ribbonBuilder = new RibbonBuilder();
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
                // We need the UIApplication, which will be available in external commands
                // We'll initialize the pipe server in the first external command
                ribbonBuilder.BuildRibbon(application);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
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
                pipeServer?.Stop();
                RuntimeManager.UnloadRuntime();
            }
            catch { }
            return Result.Succeeded;
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

        private void StartPipeServer()
        {
            if (uiapp == null)
            {
                throw new InvalidOperationException("UIApplication not initialized");
            }
            
            commandHandler = new RuntimeCommandHandler(RuntimeManager, uiapp);
            pipeServer = new PipeServer(LoaderConstants.PipeName, commandHandler.HandlePipeCommandAsync);
            pipeServer.Start();
        }
    }
}
