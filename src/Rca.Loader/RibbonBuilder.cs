using System;
using System.Reflection;
using System.Windows.Media.Imaging;
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
            try { app.CreateRibbonTab(TabName); } catch { }
            var panel = app.CreateRibbonPanel(TabName, PanelName);

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
                button.Image = LoadEmbeddedBitmap(asm, smallFileName) ?? button.Image;
                button.LargeImage = LoadEmbeddedBitmap(asm, largeFileName) ?? button.LargeImage;

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
        /// Loads a bitmap image from embedded resources.
        /// </summary>
        /// <param name="asm">The assembly containing the embedded resources.</param>
        /// <param name="fileName">The filename of the embedded resource.</param>
        /// <returns>A bitmap image, or null if not found.</returns>
        private static BitmapImage? LoadEmbeddedBitmap(Assembly asm, string fileName)
        {
            // Common resource name patterns
            // Example final: Rca.Loader.Resources.OpenAssistant16.png
            var asmName = asm.GetName().Name;
            var candidates = new[]
            {
                $"{asmName}.Resources.{fileName}",
                $"Rca.Loader.Resources.{fileName}" // fallback explicit root namespace
            };

            foreach (var resName in candidates.Distinct())
            {
                using var stream = asm.GetManifestResourceStream(resName);
                if (stream == null) continue;

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = stream;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }

            return null;
        }
    }
}