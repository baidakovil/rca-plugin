#if DEBUG
using System;
using System.Windows.Threading;
using Autodesk.Revit.UI;
using Rca.Loader.AssemblyManagement;

namespace Rca.Loader.UI
{
    /// <summary>
    /// Manages the display of assembly status information in the Revit ribbon.
    /// </summary>
    /// <remarks>
    /// This class is only available in DEBUG builds and provides visual feedback about
    /// the hot-reload system status directly in the Revit UI.
    /// </remarks>
    public class RibbonStatusDisplay
    {
        private readonly Dispatcher _uiDispatcher;
        private TextBox? _statusTextBox;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="RibbonStatusDisplay"/> class.
        /// </summary>
        public RibbonStatusDisplay()
        {
            // Store the current dispatcher for UI thread synchronization
            _uiDispatcher = Dispatcher.CurrentDispatcher;
        }
        
        /// <summary>
        /// Initializes the UI component with a TextBox.
        /// </summary>
        /// <param name="textBox">The TextBox to use for status display.</param>
        /// <exception cref="ArgumentNullException">Thrown if textBox is null.</exception>
        public void Initialize(TextBox textBox)
        {
            _statusTextBox = textBox ?? throw new ArgumentNullException(nameof(textBox));
            
            // Set initial empty status
            UpdateStatus(new LoadedAssembliesInfo());
        }
        
        /// <summary>
        /// Updates the status display with current assembly information.
        /// </summary>
        /// <param name="info">The current assembly status information.</param>
        public void UpdateStatus(LoadedAssembliesInfo info)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));
                
            if (_statusTextBox == null)
                return;
                
            // Ensure we're on the UI thread to update UI elements
            if (!_uiDispatcher.CheckAccess())
            {
                // If not on UI thread, invoke on UI thread
                _uiDispatcher.Invoke(() => UpdateStatus(info));
                return;
            }
            
            try
            {
                string loaderStatus = FormatLoaderStatus(info.LoaderComponents);
                string runtimeStatus = FormatRuntimeStatus(info.RuntimeAssembly);
                string signalStatus = FormatSignalStatus(info.LastMSBuildSignal);
                
                // Update TextBox with formatted status information
                _statusTextBox.Value = $"{loaderStatus}\n{runtimeStatus}\n{signalStatus}";
            }
            catch (Exception ex)
            {
                // Log but don't crash on UI update errors
                System.Diagnostics.Debug.WriteLine($"Error updating status display: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Formats the loader components status text.
        /// </summary>
        /// <param name="loaderComponents">The loader components information.</param>
        /// <returns>A formatted status string.</returns>
        private string FormatLoaderStatus(AssemblyInfo loaderComponents)
        {
            if (loaderComponents == null)
                return "Loader/Contracts: unknown";
                
            if (string.IsNullOrEmpty(loaderComponents.Path))
                return "Loader/Contracts: not loaded";
                
            // Extract folder name from path
            string folder = System.IO.Path.GetFileName(loaderComponents.Path);
            
            // Check if the loader is outdated by comparing with the latest in TempDllFolder
            bool isOutdated = false;
            try
            {
                var assemblyManager = LoaderApp.Instance?.AssemblyStatusManager;
                if (assemblyManager != null)
                {
                    isOutdated = assemblyManager.IsLoaderOutdated();
                }
            }
            catch
            {
                // Ignore errors in status check
            }
            
            string status = isOutdated ? "outdated" : "current";
            return $"Loader/Contracts: {status} - {folder}";
        }
        
        /// <summary>
        /// Formats the runtime assembly status text.
        /// </summary>
        /// <param name="runtimeAssembly">The runtime assembly information.</param>
        /// <returns>A formatted status string.</returns>
        private string FormatRuntimeStatus(AssemblyInfo runtimeAssembly)
        {
            if (runtimeAssembly == null)
                return "Runtime: unknown";
                
            if (string.IsNullOrEmpty(runtimeAssembly.Path))
                return "Runtime: not loaded";
                
            // Extract folder name from path
            string folder = System.IO.Path.GetFileName(
                System.IO.Path.GetDirectoryName(runtimeAssembly.Path) ?? string.Empty);
                
            // Check if the runtime is outdated
            bool isOutdated = false;
            try
            {
                var assemblyManager = LoaderApp.Instance?.AssemblyStatusManager;
                if (assemblyManager != null)
                {
                    isOutdated = assemblyManager.IsRuntimeOutdated();
                }
            }
            catch
            {
                // Ignore errors in status check
            }
            
            string status = isOutdated ? "outdated" : "current";
            return $"Runtime: {status} - {folder}";
        }
        
        /// <summary>
        /// Formats the signal status text.
        /// </summary>
        /// <param name="signalInfo">The signal information.</param>
        /// <returns>A formatted status string.</returns>
        private string FormatSignalStatus(SignalInfo signalInfo)
        {
            if (signalInfo == null)
                return "Last MSBuild signal: unknown";
                
            if (string.IsNullOrEmpty(signalInfo.Time))
                return "Last MSBuild signal: none";
                
            return $"Last MSBuild signal: {signalInfo.Time} - {signalInfo.Event}";
        }
    }
}
#endif
