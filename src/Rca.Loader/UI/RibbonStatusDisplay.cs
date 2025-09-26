#if DEBUG
using System;
using System.Windows.Threading;
using Autodesk.Revit.UI;
using Rca.Loader.AssemblyManagement;

namespace Rca.Loader.UI
{
    /// <summary>
    /// Manages the display of assembly status information in the Revit ribbon.
    /// Uses three stacked TextBox controls (one per logical line) to emulate a multi-line read-only display.
    /// </summary>
    public class RibbonStatusDisplay
    {
        private readonly Dispatcher _uiDispatcher;
        private TextBox? _line1;
        private TextBox? _line2;
        private TextBox? _line3;

        /// <summary>
        /// Initializes a new instance of the <see cref="RibbonStatusDisplay"/> class.
        /// </summary>
        public RibbonStatusDisplay()
        {
            // Store the current dispatcher for UI thread synchronization
            _uiDispatcher = Dispatcher.CurrentDispatcher;
        }

        /// <summary>
        /// Initializes the UI component with three stacked TextBox controls.
        /// </summary>
        /// <param name="line1">Top line TextBox.</param>
        /// <param name="line2">Middle line TextBox.</param>
        /// <param name="line3">Bottom line TextBox.</param>
        public void Initialize(TextBox line1, TextBox line2, TextBox line3)
        {
            _line1 = line1 ?? throw new ArgumentNullException(nameof(line1));
            _line2 = line2 ?? throw new ArgumentNullException(nameof(line2));
            _line3 = line3 ?? throw new ArgumentNullException(nameof(line3));

            try
            {
                // Configure appearance and behavior for each line
                ConfigureTextBox(_line1);
                ConfigureTextBox(_line2);
                ConfigureTextBox(_line3);
            }
            catch
            {
                // Swallow any configuration errors to avoid breaking the add-in
            }

            // Set initial empty status
            UpdateStatus(new LoadedAssembliesInfo());
        }

        private void ConfigureTextBox(TextBox tb)
        {
            try
            {
                // Try to set width to occupy more horizontal space in the ribbon
                tb.Width = 400;

                // Clear prompt text so nothing extra shows
                try { tb.PromptText = string.Empty; } catch { }

                // Make read-only if supported
                var isReadOnlyProp = tb.GetType().GetProperty("IsReadOnly");
                if (isReadOnlyProp != null && isReadOnlyProp.CanWrite)
                {
                    isReadOnlyProp.SetValue(tb, true);
                }

                // Hide the image area on the TextBox if the API exposes the property
                var showImageProp = tb.GetType().GetProperty("ShowImageAsButton");
                if (showImageProp != null && showImageProp.CanWrite)
                {
                    try { showImageProp.SetValue(tb, false); } catch { }
                }

                // Clear Image property if present
                var imageProp = tb.GetType().GetProperty("Image");
                if (imageProp != null && imageProp.CanWrite)
                {
                    try { imageProp.SetValue(tb, null); } catch { }
                }
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// Updates the status display with current assembly information.
        /// Each logical piece is shown in its own TextBox line.
        /// </summary>
        /// <param name="info">The current assembly status information.</param>
        public void UpdateStatus(LoadedAssembliesInfo info)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            // Ensure we're on the UI thread to update UI elements
            if (!_uiDispatcher.CheckAccess())
            {
                _uiDispatcher.Invoke(() => UpdateStatus(info));
                return;
            }

            try
            {
                var loaderStatus = FormatLoaderStatus(info.LoaderComponents);
                var runtimeStatus = FormatRuntimeStatus(info.RuntimeAssembly);
                var signalStatus = FormatSignalStatus(info.LastMSBuildSignal);

                if (_line1 != null) _line1.Value = loaderStatus;
                if (_line2 != null) _line2.Value = runtimeStatus;
                if (_line3 != null) _line3.Value = signalStatus;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating status display: {ex.Message}");
            }
        }

        private string FormatLoaderStatus(AssemblyInfo loaderComponents)
        {
            if (loaderComponents == null) return "Rca.Loader.dll: unknown";
            if (string.IsNullOrEmpty(loaderComponents.Path)) return "Rca.Loader.dll: Not loaded";

            string folder = System.IO.Path.GetFileName(loaderComponents.Path);
            bool isOutdated = false;
            try
            {
                var assemblyManager = LoaderApp.Instance?.AssemblyStatusManager;
                if (assemblyManager != null) isOutdated = assemblyManager.IsLoaderOutdated();
            }
            catch { }

            if (isOutdated)
            {
                return $"Rca.Loader.dll: Loaded - OUTDATED - {folder} (hash: {TruncateHash(loaderComponents.Hash)})";
            }
            else
            {
                return $"Rca.Loader.dll: Loaded - Current - {folder} (hash: {TruncateHash(loaderComponents.Hash)})";
            }
        }

        private string FormatRuntimeStatus(AssemblyInfo runtimeAssembly)
        {
            if (runtimeAssembly == null) return "Rca.Runtime.dll: unknown";
            if (string.IsNullOrEmpty(runtimeAssembly.Path)) return "Rca.Runtime.dll: Not loaded";

            string folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(runtimeAssembly.Path) ?? string.Empty);
            bool isOutdated = false;
            try
            {
                var assemblyManager = LoaderApp.Instance?.AssemblyStatusManager;
                if (assemblyManager != null) isOutdated = assemblyManager.IsRuntimeOutdated();
            }
            catch { }

            if (isOutdated)
            {
                return $"Rca.Runtime.dll: Loaded - OUTDATED - {folder} (hash: {TruncateHash(runtimeAssembly.Hash)})";
            }
            else
            {
                return $"Rca.Runtime.dll: Loaded - Current - {folder} (hash: {TruncateHash(runtimeAssembly.Hash)})";
            }
        }

        private string FormatSignalStatus(SignalInfo signalInfo)
        {
            if (signalInfo == null) return "Last MSBuild signal: null";
            if (string.IsNullOrEmpty(signalInfo.Time)) return "Last MSBuild signal: empty";
            return $"Last MSBuild signal: {signalInfo.Time} - {signalInfo.Event}";
        }

        private string TruncateHash(string? hash)
        {
            if (string.IsNullOrEmpty(hash)) return "n/a";
            return hash.Length > 8 ? hash.Substring(0, 8) : hash;
        }
    }
}
#endif
