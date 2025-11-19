#if DEBUG
using System;
using System.Windows.Threading;
using Autodesk.Revit.UI;
using Rca.Loader.AssemblyManagement;
using Rca.Loader.Logging;
using Microsoft.Extensions.Logging;

namespace Rca.Loader.UI
{
  /// <summary>
  /// Manages the display of assembly status information in the Revit ribbon.
  /// Uses three stacked TextBox controls (one per logical line) to emulate a multi-line read-only display.
  /// DEBUG-only helper: not included in release builds to minimize surface area.
  /// </summary>
  public class RibbonStatusDisplay
  {
    private static readonly ILogger Log = LoaderLog.GetLogger<RibbonStatusDisplay>();

    private readonly Dispatcher _uiDispatcher;
    private TextBox? _line1;
    private TextBox? _line2;
    private TextBox? _line3;

    /// <summary>
    /// Initializes a new instance of the <see cref="RibbonStatusDisplay"/> class.
    /// Captures current dispatcher for later UI marshalling.
    /// </summary>
    public RibbonStatusDisplay()
    {
      _uiDispatcher = Dispatcher.CurrentDispatcher;
      Log.LogTrace("RibbonStatusDisplay constructed (dispatcherHash={Hash})", _uiDispatcher.GetHashCode());
    }

    /// <summary>
    /// Initializes the UI component with three stacked TextBox controls.
    /// </summary>
    public void Initialize(TextBox line1, TextBox line2, TextBox line3)
    {
      _line1 = line1 ?? throw new ArgumentNullException(nameof(line1));
      _line2 = line2 ?? throw new ArgumentNullException(nameof(line2));
      _line3 = line3 ?? throw new ArgumentNullException(nameof(line3));

      try
      {
        ConfigureTextBox(_line1);
        ConfigureTextBox(_line2);
        ConfigureTextBox(_line3);
        Log.LogDebug("RibbonStatusDisplay text boxes configured width={Width}", _line1.Width);
      }
      catch (Exception ex)
      {
        Log.LogDebug(ex, "Non-fatal error configuring ribbon status text boxes");
      }

      UpdateStatus(new LoadedAssembliesInfo());
    }

    private void ConfigureTextBox(TextBox tb)
    {
      try
      {
        tb.Width = 400; // widen for more info
        try { tb.PromptText = string.Empty; } catch { }
        var isReadOnlyProp = tb.GetType().GetProperty("IsReadOnly");
        if (isReadOnlyProp?.CanWrite == true) isReadOnlyProp.SetValue(tb, true);
        var showImageProp = tb.GetType().GetProperty("ShowImageAsButton");
        if (showImageProp?.CanWrite == true) { try { showImageProp.SetValue(tb, false); } catch { } }
        var imageProp = tb.GetType().GetProperty("Image");
        if (imageProp?.CanWrite == true) { try { imageProp.SetValue(tb, null); } catch { } }
      }
      catch (Exception ex)
      {
        Log.LogTrace(ex, "ConfigureTextBox ignored error");
      }
    }

    /// <summary>
    /// Updates the status display with current assembly information.
    /// Each logical piece is shown in its own TextBox line.
    /// </summary>
    public void UpdateStatus(LoadedAssembliesInfo info)
    {
      if (info == null) throw new ArgumentNullException(nameof(info));

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

        Log.LogTrace("UpdateStatus loader='{Loader}' runtime='{Runtime}' signal='{Signal}'", loaderStatus, runtimeStatus, signalStatus);

        if (_line1 != null) _line1.Value = loaderStatus; else Log.LogDebug("_line1 null when updating status");
        if (_line2 != null) _line2.Value = runtimeStatus; else Log.LogDebug("_line2 null when updating status");
        if (_line3 != null) _line3.Value = signalStatus; else Log.LogDebug("_line3 null when updating status");
      }
      catch (Exception ex)
      {
        Log.LogError(ex, "Error updating ribbon status display");
      }
    }

    private string FormatLoaderStatus(AssemblyInfo loaderComponents)
    {
      if (loaderComponents == null) return "Loader Group: unknown";
      if (string.IsNullOrEmpty(loaderComponents.Path)) return "Loader Group: Not loaded";
      string folder = System.IO.Path.GetFileName(loaderComponents.Path);
      bool isOutdated = false;
      try
      {
        isOutdated = LoaderApp.Instance?.AssemblyStatusManager?.IsLoaderOutdated() ?? false;
      }
      catch { }
      return isOutdated
          ? $"Loader Group: Loaded - OUTDATED - {folder} (hash: {TruncateHash(loaderComponents.Hash)})"
          : $"Loader Group: Loaded - Current - {folder} (hash: {TruncateHash(loaderComponents.Hash)})";
    }

    private string FormatRuntimeStatus(AssemblyInfo runtimeAssembly)
    {
      if (runtimeAssembly == null) return "Runtime Group: unknown";
      if (string.IsNullOrEmpty(runtimeAssembly.Path)) return "Runtime Group: Not loaded";
      string folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(runtimeAssembly.Path) ?? string.Empty);
      bool isOutdated = false;
      try
      {
        isOutdated = LoaderApp.Instance?.AssemblyStatusManager?.IsRuntimeOutdated() ?? false;
      }
      catch { }
      return isOutdated
          ? $"Runtime Group: Loaded - OUTDATED - {folder} (hash: {TruncateHash(runtimeAssembly.Hash)})"
          : $"Runtime Group: Loaded - Current - {folder} (hash: {TruncateHash(runtimeAssembly.Hash)})";
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
