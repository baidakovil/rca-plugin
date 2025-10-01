using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Rca.Loader.Contracts;
using Autodesk.Revit.UI;
using System.Diagnostics;

namespace Rca.Loader.UI
{
    /// <summary>
    /// Minimal host control used by the Loader to register a DockablePane at Revit startup.
    /// The control displays a simple placeholder UI and allows runtime content to be swapped in.
    /// </summary>
    public class DockablePanelHost : UserControl, IRuntimePanelHost
    {
        private readonly ContentControl contentHost;

        /// <summary>
        /// Timeout for UI content swap operations. If the content set operation does not complete within this time, a placeholder will be shown.
        /// </summary>
        private static readonly TimeSpan SetContentTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Initializes a new instance of the <see cref="DockablePanelHost"/> class.
        /// </summary>
        public DockablePanelHost()
        {
            // Build a minimal placeholder UI
            var grid = new Grid
            {
                Background = System.Windows.Media.Brushes.LightGray
            };

            var text = new TextBlock
            {
                Text = "RCA: loading runtime...",
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            contentHost = new ContentControl();
            contentHost.Content = text;

            grid.Children.Add(contentHost);

            this.Content = grid;
        }

        /// <inheritdoc/>
        public void SetContent(FrameworkElement? content)
        {
            // Ensure we run on the WPF dispatcher for this control
            if (Dispatcher == null || Dispatcher.CheckAccess())
            {
                SetContentInternal(content);
                return;
            }

            try
            {
                // Use Dispatcher.Invoke with timeout to avoid hangs if runtime-provided UI misbehaves
                try
                {
                    Dispatcher.Invoke((Action)(() => SetContentInternal(content)), DispatcherPriority.Normal, SetContentTimeout);
                }
                catch (TimeoutException)
                {
                    Debug.WriteLine($"[DockablePanelHost] SetContent invoke timed out after {SetContentTimeout.TotalSeconds}s");
                    // Fallback to placeholder
                    ShowPlaceholder();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DockablePanelHost] Exception while setting content: {ex.Message}");
                ShowPlaceholder();
            }
        }

        /// <inheritdoc/>
        public FrameworkElement? GetContent()
        {
            return contentHost.Content as FrameworkElement;
        }

        private void SetContentInternal(FrameworkElement? content)
        {
            // Dispose previous content if it implements IDisposable
            if (contentHost.Content is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DockablePanelHost] Exception disposing old content: {ex.Message}");
                }
            }

            // If content is null, revert to placeholder
            if (content == null)
            {
                ShowPlaceholder();
                return;
            }

            contentHost.Content = content;
            Debug.WriteLine("[DockablePanelHost] Runtime content set successfully");
        }

        private void ShowPlaceholder()
        {
            var placeholder = new TextBlock
            {
                Text = "RCA: loading runtime...",
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            contentHost.Content = placeholder;
        }
    }
}
