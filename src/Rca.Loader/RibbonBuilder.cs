using System;
using System.Reflection;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Linq;
using Autodesk.Revit.UI;
using Rca.Loader.Commands;

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

            // Initialize command - must be called first to set up the UIApplication
            // This will be invisible to users but can be triggered by the test adapter
            app.CreateRibbonPanel(TabName, "Hidden").AddItem(new PushButtonData(
                "RCA_Initialize",
                "Initialize",
                Assembly.GetExecutingAssembly().Location,
                typeof(InitializerCommand).FullName));

            // Button: Open Standalone Window
            var openBtn = new PushButtonData(
                "RCA_OpenStandalone",
                "Open\nAssistant",
                Assembly.GetExecutingAssembly().Location,
                typeof(OpenStandaloneWindowCommand).FullName);
            var openPush = panel.AddItem(openBtn) as PushButton;
            AssignEmbeddedIcons(openPush,
                iconFileName: "OpenAssistant16.png",
                tooltip: "Open the RCA standalone assistant window.");

            // Button: Reload Runtime (latest)
            var reloadBtn = new PushButtonData(
                "RCA_ReloadRuntime",
                "Reload\nRuntime",
                Assembly.GetExecutingAssembly().Location,
                typeof(ReloadRuntimeCommand).FullName);
            var reloadPush = panel.AddItem(reloadBtn) as PushButton;
            AssignEmbeddedIcons(reloadPush,
                iconFileName: "ReloadRuntime16.png",
                tooltip: "Reload the latest deployed runtime.");
        }

        /// <summary>
        /// Assigns a 16x16 icon to both Image and LargeImage of a Revit button.
        /// </summary>
        /// <param name="button">The button to assign icons to.</param>
        /// <param name="iconFileName">The 16x16 icon filename.</param>
        /// <param name="tooltip">Optional tooltip text.</param>
        private static void AssignEmbeddedIcons(PushButton? button, string iconFileName, string? tooltip = null)
        {
            if (button == null) return;

            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var icon = GetEmbeddedImageBitmapFrame(asm, $"Rca.Loader.Resources.{iconFileName}");
                button.Image = icon;
                button.LargeImage = icon;
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
        /// Loads a bitmap image from embedded resources using BitmapFrame.Create.
        /// </summary>
        /// <param name="assembly">The assembly containing the embedded resources.</param>
        /// <param name="imageName">The resource name of the embedded image.</param>
        /// <returns>A bitmap image, or null if not found.</returns>
        private static ImageSource? GetEmbeddedImageBitmapFrame(Assembly assembly, string imageName)
        {
            try
            {
                var stream = assembly.GetManifestResourceStream(imageName);
                if (stream == null) return null;
                var imageFrame = BitmapFrame.Create(stream);
                return imageFrame;
            }
            catch
            {
                return null;
            }
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
    }
}