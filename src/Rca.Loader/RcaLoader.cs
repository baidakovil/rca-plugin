using System;
using Autodesk.Revit.UI;
using Rca.Loader.Contracts;

namespace Rca.Loader
{
    /// <summary>
    /// Helper class for RcaPlugin to initialize the loader.
    /// </summary>
    public class RcaLoader
    {
        private RuntimeManager runtimeManager;
        
        /// <summary>
        /// Initializes a new instance of the RcaLoader class.
        /// </summary>
        public RcaLoader()
        {
            runtimeManager = new RuntimeManager();
        }
        
        /// <summary>
        /// Initializes the loader with the Revit application.
        /// </summary>
        /// <param name="application">The Revit UI application.</param>
        public void Initialize(UIControlledApplication application)
        {
            // Add any initialization code needed for the plugin
            LoadLatestRuntime();
        }
        
        /// <summary>
        /// Loads the latest runtime version.
        /// </summary>
        private void LoadLatestRuntime()
        {
            try
            {
                if (runtimeManager.ReloadLatest(out string? error))
                {
                    // Runtime loaded successfully
                }
                else if (!string.IsNullOrEmpty(error))
                {
                    TaskDialog.Show("RCA Loader", $"Failed to load runtime: {error}");
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Loader Error", $"Failed to load runtime: {ex.Message}");
            }
        }
    }
}