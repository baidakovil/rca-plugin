using System;
using System.Reflection;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Autodesk.Revit.UI;
using Rca.Loader.Commands;
using Rca.Loader.Contracts;
using Rca.Loader.Logging;
using Microsoft.Extensions.Logging;
#if DEBUG
using Rca.Loader.UI;
#endif

namespace Rca.Loader.Services
{
    /// <summary>
    /// Handles creation and configuration of Revit ribbon UI components.
    /// In DEBUG builds, adds controls to standard Add-Ins tab for development.
    /// </summary>
    public class RibbonService : IRibbonService
    {
        private const string PanelName = "RCA Debug";
        private static readonly ILogger Log = LoaderLog.GetLogger<RibbonService>();

#if DEBUG
        /// <summary>
        /// Gets the status display for the ribbon.
        /// </summary>
        public RibbonStatusDisplay? StatusDisplay { get; private set; }
#endif

        /// <summary>
        /// Builds the RCA ribbon controls in Revit's standard Add-Ins tab.
        /// Uses the built-in Tab.AddIns constant to ensure proper tab reference.
        /// </summary>
        /// <param name="application">The Revit UI controlled application.</param>
        public void BuildRibbon(object application)
        {
            if (application is not UIControlledApplication uiApp)
                throw new ArgumentException("Application must be a UIControlledApplication", nameof(application));

#if DEBUG
            // In DEBUG builds, add controls to standard Add-Ins tab using the built-in constant
            try
            {
                // Use the built-in Revit API constant for Add-Ins tab
                var panel = uiApp.CreateRibbonPanel(Tab.AddIns, PanelName);
                Log.LogInformation("Ribbon panel created in Add-Ins tab, panel={Panel}", PanelName);

                // Button: Show RCA Panel
                var showPanelBtn = new PushButtonData(
                    "RCA_ShowPanel",
                    "Show\nRCA Panel",
                    Assembly.GetExecutingAssembly().Location,
                    typeof(ShowDockablePanelCommand).FullName);
                var showPanelPush = panel.AddItem(showPanelBtn) as PushButton;
                AssignEmbeddedIcons(showPanelPush,
                    iconFileName: "OpenAssistant16.png",
                    tooltip: "Show the RCA Chat Assistant panel");
                Log.LogDebug("Show panel button added to {Panel}", PanelName);

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
                Log.LogDebug("Reload runtime button added to {Panel}", PanelName);

                // Add separator for visual grouping
                panel.AddSeparator();

                // Create three stacked status TextBoxes
                TextBoxData tb1 = new TextBoxData("RCA_StatusLine1") 
                { 
                    Name = "Assembly Status", 
                    ToolTip = "Shows status of loaded assemblies" 
                };
                TextBoxData tb2 = new TextBoxData("RCA_StatusLine2") 
                { 
                    Name = "Runtime Status",
                    ToolTip = "Shows runtime assembly information"
                };
                TextBoxData tb3 = new TextBoxData("RCA_StatusLine3") 
                { 
                    Name = "Signal Status",
                    ToolTip = "Shows MSBuild signal information"
                };

                // Add them as stacked items
                var items = panel.AddStackedItems(tb1, tb2, tb3);

                if (items == null)
                {
                    Log.LogWarning("AddStackedItems returned null for debug textboxes");
                    return;
                }

                // The returned list maps to the created controls in the same order
                TextBox? line1 = items.Count > 0 ? items[0] as TextBox : null;
                TextBox? line2 = items.Count > 1 ? items[1] as TextBox : null;
                TextBox? line3 = items.Count > 2 ? items[2] as TextBox : null;

                if (line1 == null || line2 == null || line3 == null)
                {
                    Log.LogWarning("One or more ribbon textboxes are null line1={L1} line2={L2} line3={L3}", 
                        line1 != null, line2 != null, line3 != null);
                    return;
                }

                StatusDisplay = new RibbonStatusDisplay();
                StatusDisplay.Initialize(line1, line2, line3);
                Log.LogInformation("Debug status display initialized in {Panel}", PanelName);
            }
            catch (Exception ex)
            {
                Log.LogWarning(ex, "Failed to create debug UI in Add-Ins tab");
            }
#else
            // In RELEASE builds, do nothing - no UI needed
            Log.LogInformation("RELEASE build - no ribbon UI created");
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
