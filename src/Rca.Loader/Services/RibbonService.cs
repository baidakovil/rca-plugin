using System;
using System.Reflection;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Autodesk.Revit.UI;
using Rca.Loader.Commands;
using Rca.Loader.Contracts;
#if DEBUG
using Rca.Loader.UI;
#endif

namespace Rca.Loader.Services
{
    /// <summary>
    /// Handles creation and configuration of Revit ribbon UI components.
    /// </summary>
    public class RibbonService : IRibbonService
    {
        private const string TabName = "RCA";
        private const string PanelName = "Loader";
        
#if DEBUG
        /// <summary>
        /// Gets the status display for the ribbon.
        /// </summary>
        public RibbonStatusDisplay? StatusDisplay { get; private set; }
#endif

        /// <summary>
        /// Builds the RCA ribbon tab and panels in Revit.
        /// </summary>
        /// <param name="application">The Revit UI controlled application.</param>
        public void BuildRibbon(object application)
        {
            if (application is not UIControlledApplication uiApp)
            {
                throw new ArgumentException("Application must be a UIControlledApplication", nameof(application));
            }

            try { uiApp.CreateRibbonTab(TabName); } catch { }
            var panel = uiApp.CreateRibbonPanel(TabName, PanelName);

            // Initialize command - must be called first to set up the UIApplication
            // This will be invisible to users but can be triggered by the test adapter
            uiApp.CreateRibbonPanel(TabName, "Hidden").AddItem(new PushButtonData(
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
                
#if DEBUG
            // Create Debug panel and three stacked status TextBoxes (only in DEBUG builds)
            try
            {
                var debugPanel = uiApp.CreateRibbonPanel(TabName, "Debug");

                // Do not assign images to TextBoxData to avoid icons displaying next to textboxes
                TextBoxData tb1 = new TextBoxData("RCA_StatusLine1") { Name = "Assembly Status", ToolTip = "Shows status of loaded assemblies" };
                TextBoxData tb2 = new TextBoxData("RCA_StatusLine2") { Name = "Runtime Status", ToolTip = string.Empty };
                TextBoxData tb3 = new TextBoxData("RCA_StatusLine3") { Name = "Signal Status", ToolTip = string.Empty };

                // Add them as stacked items
                var items = debugPanel.AddStackedItems(tb1, tb2, tb3);

                if (items == null)
                {
                    System.Diagnostics.Debug.WriteLine("Debug: AddStackedItems returned null for debug textboxes");
                }

                // The returned list maps to the created controls in the same order
                TextBox? line1 = items != null && items.Count > 0 ? items[0] as TextBox : null;
                TextBox? line2 = items != null && items.Count > 1 ? items[1] as TextBox : null;
                TextBox? line3 = items != null && items.Count > 2 ? items[2] as TextBox : null;

                if (line1 == null || line2 == null || line3 == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Debug: One or more ribbon textboxes are null: line1={line1!=null}, line2={line2!=null}, line3={line3!=null}");
                }

                if (line1 != null && line2 != null && line3 != null)
                {
                    StatusDisplay = new RibbonStatusDisplay();
                    StatusDisplay.Initialize(line1, line2, line3);

                    // Set tooltip on first textbox
                    line1.ToolTip = "Shows status of loaded assemblies";
                }
            }
            catch (Exception ex)
            {
                // Log but don't crash if debug UI creation fails
                System.Diagnostics.Debug.WriteLine($"Failed to create debug UI: {ex.Message}\n{ex.StackTrace}");
            }
#endif
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
            catch
            {
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
    }
}
