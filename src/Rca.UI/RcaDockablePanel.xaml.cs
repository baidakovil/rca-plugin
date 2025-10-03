using Autodesk.Revit.UI;
using Rca.Contracts;
using Rca.UI.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Rca.UI.Views
{
    /// <summary>
    /// Interaction logic for RcaDockablePanel.xaml
    /// </summary>
    public partial class RcaDockablePanel : UserControl
    {
        public RcaDockablePanel(
            Func<UIApplication?> uiappProvider, 
            IPythonExecutionService pythonService)
        {
            try
            {
                // Load XAML manually rather than relying on the automatic XAML loading
                // This is necessary when the assembly is merged with ILRepack
                LoadXaml();
                
                // Handle potential null services in standalone mode
                if (pythonService == null)
                {
                    Debug.WriteLine("Warning: RcaDockablePanel created with null pythonService");
                    pythonService = new NullPythonExecutionService();
                }
                
                DataContext = new RcaDockablePanelViewModel(uiappProvider, pythonService);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing RcaDockablePanel: {ex.Message}");
                MessageBox.Show(
                    $"Error initializing RcaDockablePanel: {ex.Message}\n\n{ex.StackTrace}",
                    "RcaDockablePanel Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                
                // Create a minimal UI showing the error
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
        /// Loads the XAML content for this control manually
        /// </summary>
        private void LoadXaml()
        {
            try
            {
                // First try to load from embedded resource (works after ILRepack)
                string? xamlContent = GetEmbeddedXaml();
                if (!string.IsNullOrEmpty(xamlContent))
                {
                    // Remove or replace the x:Class directive to avoid the type mismatch error
                    xamlContent = RemoveClassDirective(xamlContent);
                    LoadFromXamlString(xamlContent);
                    return;
                }
                
                // Fallback to standard InitializeComponent (works in design-time and normal runtime)
                InitializeComponent();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading XAML: {ex.Message}");
                throw new InvalidOperationException($"Failed to load XAML for RcaDockablePanel: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Removes or modifies the x:Class directive from the XAML string to avoid type matching issues
        /// </summary>
        private string RemoveClassDirective(string xamlContent)
        {
            // Use regex to remove the x:Class attribute
            string pattern = @"x:Class=""[^""]+""";
            string result = Regex.Replace(xamlContent, pattern, "");
            
            return result;
        }
        
        /// <summary>
        /// Gets the embedded XAML content from the assembly resources
        /// </summary>
        private string? GetEmbeddedXaml()
        {
            // Try several potential resource names since ILRepack might change them
            var resourceNames = new[]
            {
                "Rca.UI.RcaDockablePanel.xaml",
                "Rca.UI.Views.RcaDockablePanel.xaml",
                "Rca.Runtime.Rca.UI.RcaDockablePanel.xaml",
                "Rca.Runtime.Rca.UI.Views.RcaDockablePanel.xaml"
            };
            
            var assembly = GetType().Assembly;
            Debug.WriteLine($"Looking for XAML in assembly: {assembly.FullName}");
            
            foreach (var resourceName in resourceNames)
            {
                Debug.WriteLine($"Trying to load resource: {resourceName}");
                
                try
                {
                    using (var stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            using (var reader = new StreamReader(stream))
                            {
                                string xaml = reader.ReadToEnd();
                                Debug.WriteLine($"Successfully loaded XAML resource: {resourceName}");
                                return xaml;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading resource {resourceName}: {ex.Message}");
                }
            }
            
            // List all available resources for debugging
            Debug.WriteLine("Available resources:");
            foreach (var resource in assembly.GetManifestResourceNames())
            {
                Debug.WriteLine($" - {resource}");
            }
            
            return null;
        }
        
        /// <summary>
        /// Loads this control from a XAML string
        /// </summary>
        private void LoadFromXamlString(string xamlContent)
        {
            Debug.WriteLine("Loading XAML from string");
            
            var context = new ParserContext();
            context.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            context.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");
            context.XmlnsDictionary.Add("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            context.XmlnsDictionary.Add("d", "http://schemas.microsoft.com/expression/blend/2008");
            
            try
            {
                // Parse the XAML content
                object content = XamlReader.Parse(xamlContent, context);
                
                if (content is Grid grid)
                {
                    Debug.WriteLine("Parsed content is a Grid, setting as Content");
                    this.Content = grid;
                }
                else if (content is UserControl userControl)
                {
                    Debug.WriteLine("Parsed content is a UserControl, copying Content");
                    this.Content = userControl.Content;
                }
                else
                {
                    Debug.WriteLine($"Parsed content is a {content.GetType().Name}, setting as Content");
                    this.Content = content;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing XAML: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Null implementation for standalone mode
        /// </summary>
        private class NullPythonExecutionService : IPythonExecutionService
        {
            public Task<string> ExecuteAsync(string code)
            {
                return Task.FromResult(
                    "Python execution not available in standalone mode.");
            }

            public string ExecuteSync(string code)
            {
                return "Python execution not available in standalone mode.";
            }

            public void SetRevitContext(object context)
            {
                // Do nothing
            }
        }
    }
}
