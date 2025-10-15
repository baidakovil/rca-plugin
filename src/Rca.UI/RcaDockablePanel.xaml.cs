using Autodesk.Revit.UI;
using Rca.Contracts;
using Rca.UI.ViewModels;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Microsoft.Extensions.Logging;
using Rca.UI.Logging; // UI dynamic logging adapter

namespace Rca.UI.Views
{
    /// <summary>
    /// Interaction logic for RcaDockablePanel.xaml.
    /// Uses UiLog adapter to send logs to unified pipeline when Runtime logging provider is available.
    /// </summary>
    public partial class RcaDockablePanel : UserControl
    {
        private static readonly ILogger Log = UiLog.GetLogger<RcaDockablePanel>();

        public RcaDockablePanel(
            Func<UIApplication?> uiappProvider,
            IPythonExecutionService pythonService)
        {
            try
            {
                Log.LogDebug("Constructing panel instance");
                LoadXaml();

                if (pythonService == null)
                {
                    Log.LogWarning("Panel created with null pythonService - using NullPythonExecutionService");
                    pythonService = new NullPythonExecutionService();
                }

                DataContext = new RcaDockablePanelViewModel(uiappProvider, pythonService);
                Log.LogInformation("Panel DataContext assigned (vmType={VmType})", DataContext?.GetType().FullName);
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "Error initializing RcaDockablePanel");
                MessageBox.Show(
                    $"Error initializing RcaDockablePanel: {ex.Message}\n\n{ex.StackTrace}",
                    "RcaDockablePanel Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

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

        private void LoadXaml()
        {
            // Prefer compiled BAML to guarantee that XAML changes are picked up after rebuild
            try
            {
                Log.LogTrace("Loading XAML via InitializeComponent (compiled BAML)");
                InitializeComponent();
                return;
            }
            catch (Exception initEx)
            {
                Log.LogDebug(initEx, "InitializeComponent failed, attempting embedded XAML fallback");
            }

            // Fallback to embedded XAML text if available
            try
            {
                string? xamlContent = GetEmbeddedXaml();
                if (!string.IsNullOrEmpty(xamlContent))
                {
                    xamlContent = RemoveClassDirective(xamlContent);
                    LoadFromXamlString(xamlContent);
                    Log.LogDebug("Loaded XAML from embedded resource (length={Len})", xamlContent.Length);
                    return;
                }

                Log.LogWarning("Embedded XAML not found and InitializeComponent failed - UI may be empty");
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "Error loading XAML from embedded resource");
                throw new InvalidOperationException($"Failed to load XAML for RcaDockablePanel: {ex.Message}", ex);
            }
        }

        private string RemoveClassDirective(string xamlContent)
        {
            const string pattern = "x:Class=\"[^\"]+\"";
            return Regex.Replace(xamlContent, pattern, "");
        }

        private string? GetEmbeddedXaml()
        {
            var resourceNames = new[]
            {
                "Rca.UI.RcaDockablePanel.xaml",
                "Rca.UI.Views.RcaDockablePanel.xaml",
                "Rca.Runtime.Rca.UI.RcaDockablePanel.xaml",
                "Rca.Runtime.Rca.UI.Views.RcaDockablePanel.xaml"
            };

            var assembly = GetType().Assembly;
            Log.LogDebug("Searching XAML resources assembly={Assembly}", assembly.FullName);

            foreach (var resourceName in resourceNames)
            {
                try
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream == null) continue;
                    using var reader = new StreamReader(stream);
                    var xaml = reader.ReadToEnd();
                    Log.LogDebug("Loaded embedded XAML resource {Resource} size={Size}", resourceName, xaml.Length);
                    return xaml;
                }
                catch (Exception ex)
                {
                    Log.LogDebug(ex, "Failed loading resource {Resource}", resourceName);
                }
            }

            Log.LogDebug("Embedded XAML not found (listing resources)");
            foreach (var res in assembly.GetManifestResourceNames())
                Log.LogTrace("Resource: {Name}", res);
            return null;
        }

        private void LoadFromXamlString(string xamlContent)
        {
            Log.LogTrace("Parsing XAML string");
            var context = new ParserContext();
            context.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            context.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");
            context.XmlnsDictionary.Add("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            context.XmlnsDictionary.Add("d", "http://schemas.microsoft.com/expression/blend/2008");
            try
            {
                object content = XamlReader.Parse(xamlContent, context);
                switch (content)
                {
                    case Grid grid:
                        Content = grid;
                        Log.LogDebug("Parsed XAML as Grid");
                        break;
                    case UserControl uc:
                        Content = uc.Content;
                        Log.LogDebug("Parsed XAML as UserControl contentType={Type}", uc.Content?.GetType().Name);
                        break;
                    default:
                        Content = content;
                        Log.LogDebug("Parsed XAML as {Type}", content.GetType().Name);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "Error parsing XAML string");
                throw;
            }
        }

        private class NullPythonExecutionService : IPythonExecutionService
        {
            public Task<string> ExecuteAsync(string code) => Task.FromResult("Python execution not available in standalone mode.");
            public string ExecuteSync(string code) => "Python execution not available in standalone mode.";
            public void SetRevitContext(object context) { }
        }
    }
}
