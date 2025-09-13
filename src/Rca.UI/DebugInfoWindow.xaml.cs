#nullable enable
using Rca.Contracts;
using Rca.UI.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Rca.UI.Views
{
    /// <summary>
    /// Interaction logic for DebugInfoWindow.xaml
    /// </summary>
    public partial class DebugInfoWindow : Window
    {
        public DebugInfoWindow(IDebugLogService debugLogService)
        {
            if (debugLogService is null)
                throw new ArgumentNullException(nameof(debugLogService));
            try
            {
                // Load XAML manually rather than relying on the automatic XAML loading
                LoadXaml();
                DataContext = new DebugInfoViewModel(debugLogService);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing DebugInfoWindow: {ex.Message}");
                MessageBox.Show(
                    $"Error initializing DebugInfoWindow: {ex.Message}",
                    "Debug Window Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                
                // Create a minimal UI showing the error
                Title = "Debug Info (Error)";
                Content = new TextBlock
                {
                    Text = $"Error loading Debug Info window: {ex.Message}",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
        }
        
        /// <summary>
        /// Loads the XAML content for this window manually
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
                throw new InvalidOperationException($"Failed to load XAML for DebugInfoWindow: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Removes or modifies the x:Class directive from the XAML string to avoid type matching issues
        /// </summary>
        private string RemoveClassDirective(string xamlContent)
        {
            Debug.WriteLine("Removing x:Class directive from XAML");
            // Use regex to remove the x:Class attribute (fix: escape quotes properly)
            string pattern = "x:Class=\"[^\"]+\"";
            string result = Regex.Replace(xamlContent, pattern, string.Empty);
            return result;
        }
        
        /// <summary>
        /// Gets the embedded XAML content from the assembly resources
        /// </summary>
        private string? GetEmbeddedXaml()
        {
            // Try several potential resource names since ILRepack might change them
            var resourceNames = new string[]
            {
                "Rca.UI.DebugInfoWindow.xaml",
                "Rca.UI.Views.DebugInfoWindow.xaml",
                "Rca.Runtime.Rca.UI.DebugInfoWindow.xaml",
                "Rca.Runtime.Rca.UI.Views.DebugInfoWindow.xaml"
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
        /// Loads this window from a XAML string
        /// </summary>
        private void LoadFromXamlString(string xamlContent)
        {
            var context = new ParserContext();
            context.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            context.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");
            
            try
            {
                // Parse the XAML string
                object? content = XamlReader.Parse(xamlContent, context);
                
                if (content is Window window)
                {
                    // Copy properties from the loaded window to this window
                    this.Title = window.Title;
                    this.Width = window.Width;
                    this.Height = window.Height;
                    this.Content = window.Content;
                    this.Style = window.Style;
                    this.Resources = window.Resources;
                }
                else if (content is Grid grid)
                {
                    // If we just got a grid (which might happen if the x:Class was removed),
                    // then use it as our content
                    this.Content = grid;
                }
                else if (content is not null)
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
    }
}
