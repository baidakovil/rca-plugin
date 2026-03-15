using Autodesk.Revit.UI;
using Rca.Contracts;
using Rca.UI.ViewModels;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Microsoft.Extensions.Logging;
using Rca.UI.Logging;

namespace Rca.UI.Views
{
  /// <summary>
  /// Dockable panel view. Loads XAML deterministically from a known embedded resource.
  /// </summary>
  public partial class RcaDockablePanel : UserControl
  {
    private static readonly ILogger Log = UiLog.GetLogger<RcaDockablePanel>();
    private const string ManifestXamlResource = "Rca.UI.RcaDockablePanel.xaml";

    public RcaDockablePanel(Func<UIApplication?> uiappProvider, IPythonExecutionService pythonService)
    {
      try
      {
        LoadXamlFromResource();

        if (pythonService == null)
          pythonService = new NullPythonExecutionService();

        DataContext = new RcaDockablePanelViewModel(uiappProvider, pythonService);
        Log.LogInformation("Panel DataContext assigned (vmType={VmType})", DataContext?.GetType().FullName);
      }
      catch (Exception ex)
      {
        Log.LogError(ex, "Error initializing RcaDockablePanel");
        Content = new TextBlock
        {
          Text = $"Error loading UI: {ex.Message}",
          TextWrapping = TextWrapping.Wrap,
          Margin = new Thickness(10),
          HorizontalAlignment = HorizontalAlignment.Center,
          VerticalAlignment = VerticalAlignment.Center
        };
      }
    }

    /// <summary>
    /// Loads the panel content from embedded XAML.
    /// </summary>
    private void LoadXamlFromResource()
    {
      var asm = GetType().Assembly;
      using var s = asm.GetManifestResourceStream(ManifestXamlResource)
          ?? throw new InvalidOperationException($"Manifest resource not found: {ManifestXamlResource}");
      using var sr = new StreamReader(s, Encoding.UTF8);
      var xaml = sr.ReadToEnd();

      var sanitized = StripXClass(xaml);
      var ctx = new ParserContext();
      ctx.XmlnsDictionary.Add(string.Empty, "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
      ctx.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");
      ctx.XmlnsDictionary.Add("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
      ctx.XmlnsDictionary.Add("d", "http://schemas.microsoft.com/expression/blend/2008");

      var content = XamlReader.Parse(sanitized, ctx);
      ApplyLoadedContent(content);
    }

    private static string StripXClass(string xaml)
    {
      if (string.IsNullOrEmpty(xaml)) return xaml;
      const string marker = "x:Class=\"";
      var idx = xaml.IndexOf(marker, StringComparison.Ordinal);
      if (idx < 0) return xaml;
      var start = idx;
      var end = xaml.IndexOf('"', start + marker.Length);
      if (end < 0) return xaml;
      int removeEnd = end + 1;
      while (removeEnd < xaml.Length && char.IsWhiteSpace(xaml[removeEnd])) removeEnd++;
      return xaml.Remove(start, removeEnd - start);
    }

    private void ApplyLoadedContent(object? content)
    {
      if (content is Grid grid)
      {
        Content = grid;
      }
      else if (content is UserControl uc)
      {
        Content = uc.Content;
      }
      else
      {
        Content = content as UIElement;
      }
    }

    private class NullPythonExecutionService : IPythonExecutionService
    {
      public PythonRuntimeStatus GetRuntimeStatus() => PythonRuntimeStatus.Unavailable("Python execution not available in standalone mode.");
      public Task<string> ExecuteAsync(string code) => Task.FromResult("Python execution not available in standalone mode.");
      public string ExecuteSync(string code) => "Python execution not available in standalone mode.";
      public void SetRevitContext(object context) { }
    }
  }
}
