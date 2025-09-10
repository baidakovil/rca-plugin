using System;
using System.Reflection;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Linq;
using Autodesk.Revit.UI;
using Rca.Loader.Commands;
using Autodesk.Windows;

namespace Rca.Loader
{
    /// <summary>
    /// Handles creation and configuration of Revit ribbon UI components.
    /// </summary>
    public class RibbonBuilder
    {
        private const string TabName = "RCA";
        private const string PanelName = "Loader";

        /// <summary>
        /// Builds the RCA ribbon tab and buttons in the Revit UI.
        /// </summary>
        /// <param name="app">The Revit UI application.</param>
        public void BuildRibbon(UIControlledApplication app)
        {
            // Test embedded resources first
            TestEmbeddedResourcesAccess();
            
            try { app.CreateRibbonTab(TabName); } catch { }
            var panel = app.CreateRibbonPanel(TabName, PanelName);

#if DEBUG
            // Debug: List all embedded resources to help troubleshoot icon loading
            LogEmbeddedResources();
#endif

            // Button: Open Standalone Window
            var openBtn = new PushButtonData(
                "RCA_OpenStandalone",
                "Open\nAssistant",
                Assembly.GetExecutingAssembly().Location,
                typeof(OpenStandaloneWindowCommand).FullName);
            var openPush = panel.AddItem(openBtn) as PushButton;
            AssignEmbeddedIcons(openPush,
                smallFileName: "OpenAssistant16.png",
                largeFileName: "OpenAssistant32.png",
                tooltip: "Open the RCA standalone assistant window.");

            // Button: Reload Runtime (latest)
            var reloadBtn = new PushButtonData(
                "RCA_ReloadRuntime",
                "Reload\nRuntime",
                Assembly.GetExecutingAssembly().Location,
                typeof(ReloadRuntimeCommand).FullName);
            var reloadPush = panel.AddItem(reloadBtn) as PushButton;
            AssignEmbeddedIcons(reloadPush,
                smallFileName: "ReloadRuntime16.png",
                largeFileName: "ReloadRuntime32.png",
                tooltip: "Reload the latest deployed runtime.");
        }

        /// <summary>
        /// Tests embedded resources access and shows results in TaskDialog
        /// </summary>
        private static void TestEmbeddedResourcesAccess()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var resources = asm.GetManifestResourceNames();
                
                var message = $"Assembly: {asm.GetName().Name}\n";
                message += $"Location: {asm.Location}\n";
                message += $"Total embedded resources: {resources.Length}\n\n";
                
                if (resources.Length == 0)
                {
                    message += "? NO EMBEDDED RESOURCES FOUND!\n";
                    message += "This means the PNG files are not being included in the build.";
                }
                else
                {
                    message += "Found resources:\n";
                    foreach (var resource in resources)
                    {
                        message += $"  • {resource}\n";
                        
                        // Test if we can actually open the resource
                        try
                        {
                            using var stream = asm.GetManifestResourceStream(resource);
                            if (stream != null)
                            {
                                message += $"    ? Accessible ({stream.Length} bytes)\n";
                            }
                            else
                            {
                                message += $"    ? Stream is null\n";
                            }
                        }
                        catch (Exception ex)
                        {
                            message += $"    ? Error: {ex.Message}\n";
                        }
                    }
                }
                
                // Show the results in a TaskDialog
                TaskDialog.Show("RCA Icon Resources Test", message);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Icon Resources Error", $"Error testing embedded resources: {ex.Message}");
            }
        }

#if DEBUG
        /// <summary>
        /// Logs all embedded resources in the current assembly for debugging purposes.
        /// </summary>
        private static void LogEmbeddedResources()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var resources = asm.GetManifestResourceNames();
                System.Diagnostics.Debug.WriteLine($"Assembly: {asm.GetName().Name}");
                System.Diagnostics.Debug.WriteLine($"Total embedded resources: {resources.Length}");
                foreach (var resource in resources)
                {
                    System.Diagnostics.Debug.WriteLine($"  - {resource}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error listing embedded resources: {ex.Message}");
            }
        }
#endif

        /// <summary>
        /// Assigns icons to a Revit button.
        /// </summary>
        /// <param name="button">The button to assign icons to.</param>
        /// <param name="smallFileName">The small icon filename.</param>
        /// <param name="largeFileName">The large icon filename.</param>
        /// <param name="tooltip">Optional tooltip text.</param>
        private static void AssignEmbeddedIcons(PushButton? button, string smallFileName, string largeFileName, string? tooltip = null)
        {
            if (button == null) return;

            try
            {
                var asm = Assembly.GetExecutingAssembly();
                
                var smallImage = LoadEmbeddedBitmap(asm, smallFileName);
                var largeImage = LoadEmbeddedBitmap(asm, largeFileName);
                
                if (smallImage != null)
                {
                    button.Image = smallImage;
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"Successfully loaded small icon: {smallFileName}");
#endif
                }
                else
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"Failed to load small icon: {smallFileName}");
#endif
                }
                
                if (largeImage != null)
                {
                    button.LargeImage = largeImage;
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"Successfully loaded large icon: {largeFileName}");
#endif
                }
                else
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"Failed to load large icon: {largeFileName}");
#endif
                }

                if (!string.IsNullOrWhiteSpace(tooltip))
                {
                    button.ToolTip = tooltip;
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Exception loading icons: {ex.Message}");
#endif
                // Ignore icon load issues to avoid blocking add-in load.
            }
        }

        /// <summary>
        /// Loads a bitmap image from embedded resources.
        /// </summary>
        /// <param name="asm">The assembly containing the embedded resources.</param>
        /// <param name="fileName">The filename of the embedded resource.</param>
        /// <returns>A bitmap image, or null if not found.</returns>
        private static ImageSource? LoadEmbeddedBitmap(Assembly asm, string fileName)
        {
            try
            {
#if DEBUG
                // Debug: List all embedded resources in the assembly
                var allResources = asm.GetManifestResourceNames();
                System.Diagnostics.Debug.WriteLine($"Assembly: {asm.GetName().Name}");
                System.Diagnostics.Debug.WriteLine($"All embedded resources: {string.Join(", ", allResources)}");
#endif

                // Try multiple resource name patterns
                var resourceNames = new[]
                {
                    $"Rca.Loader.Resources.{fileName}",  // Explicit LogicalName pattern
                    $"{asm.GetName().Name}.Resources.{fileName}",  // Assembly name pattern
                    fileName  // Direct filename (fallback)
                };

                foreach (var resourceName in resourceNames)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"Trying resource name: {resourceName}");
#endif
                    var stream = asm.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
#if DEBUG
                        System.Diagnostics.Debug.WriteLine($"Successfully found resource: {resourceName}");
#endif
                        // Use BitmapFrame.Create as recommended for Revit ribbon icons
                        var imageFrame = BitmapFrame.Create(stream);
                        return imageFrame;
                    }
                }

#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Resource not found with any naming pattern: {fileName}");
#endif
                return null;
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Exception loading embedded resource {fileName}: {ex.Message}");
#endif
                return null;
            }
        }
    }
}