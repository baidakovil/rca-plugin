using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Rca.Loader.Contracts;
using Autodesk.Revit.UI;
using Rca.Loader.Logging; // unified logging
using Microsoft.Extensions.Logging;

namespace Rca.Loader.UI
{
  /// <summary>
  /// Minimal host control used by the Loader to register a DockablePane at Revit startup.
  /// Provides a placeholder and later swaps in runtime-provided WPF content.
  /// to ensure visibility in central log files and chronological correlation with runtime events.
  /// </summary>
  public class DockablePanelHost : UserControl, IRuntimePanelHost
  {
    private static readonly ILogger Log = LoaderLog.GetLogger<DockablePanelHost>();
    private readonly ContentControl contentHost;
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
      Log.LogDebug("DockablePanelHost constructed with placeholder");
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
          Log.LogWarning("SetContent invoke timed out after {Seconds}s; showing placeholder", SetContentTimeout.TotalSeconds);
          // Fallback to placeholder
          ShowPlaceholder();
        }
      }
      catch (Exception ex)
      {
        Log.LogError(ex, "Exception while setting content; reverting to placeholder");
        ShowPlaceholder();
      }
    }

    /// <inheritdoc/>
    public FrameworkElement? GetContent() => contentHost.Content as FrameworkElement;

    private void SetContentInternal(FrameworkElement? content)
    {
      // Dispose previous content if it implements IDisposable
      if (contentHost.Content is IDisposable disposable)
      {
        try { disposable.Dispose(); }
        catch (Exception ex) { Log.LogDebug(ex, "Exception disposing previous content"); }
      }

      // If content is null, revert to placeholder
      if (content == null)
      {
        Log.LogDebug("SetContentInternal received null – restoring placeholder");
        ShowPlaceholder();
        return;
      }

      contentHost.Content = content;
      Log.LogInformation("Runtime content set successfully (type={Type})", content.GetType().FullName);
    }

    private void ShowPlaceholder()
    {
      contentHost.Content = new TextBlock
      {
        Text = "RCA: loading runtime...",
        Margin = new Thickness(10),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
      };
      Log.LogTrace("Placeholder content displayed");
    }
  }
}
