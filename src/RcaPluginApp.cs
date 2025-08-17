            using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows;
using System.Reflection;
using System.IO;

namespace RcaPlugin
{
    /// <summary>
    /// The main external application class for the RCA Plugin.
    /// Implements IExternalApplication to register dockable panel and ribbon button.
    /// </summary>
    public class RcaPluginApp : IExternalApplication
    {
        private const string AddInId = "8B2A1C3D-4E5F-47A8-9B0C-1234567890AB";
        private const string DockablePaneGuid = "A1B2C3D4-E5F6-47A8-9B0C-1234567890AB";
        private const string DockablePaneName = "RCA Chat Assistant";
        private const string RibbonTabName = "RCA Plugin";
        private const string RibbonPanelName = "Chat Panel";
        private const string ButtonName = "ShowChatPanel";
        private const string ButtonText = "Chat Assistant";
        private const string ButtonTooltip = "Open the RCA Chat Assistant panel.";
        private const string IconPath = @"Resources/rca-icon.png";

        /// <summary>
        /// Called when Revit starts up. Registers dockable panel and ribbon button.
        /// </summary>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Create ribbon tab if not exists
                try { application.CreateRibbonTab(RibbonTabName); } catch { }
                var panel = application.CreateRibbonPanel(RibbonTabName, RibbonPanelName);

                // Create push button for panel
                var buttonData = new PushButtonData(ButtonName, ButtonText, typeof(RcaPluginApp).Assembly.Location, typeof(RcaPlugin.Commands.ShowDockablePanelCommand).FullName)
                {
                    ToolTip = ButtonTooltip,
                    LargeImage = LoadPngIcon(IconPath)
                };
                panel.AddItem(buttonData);

                // Register dockable pane
                var dpId = new DockablePaneId(new Guid(DockablePaneGuid));
                var provider = new RcaPlugin.Views.RcaDockablePanelProvider();
                application.RegisterDockablePane(dpId, DockablePaneName, provider);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Plugin Error", ex.Message);
                return Result.Failed;
            }
            return Result.Succeeded;
        }

        /// <summary>
        /// Called when Revit shuts down. Cleanup if needed.
        /// </summary>
        public Result OnShutdown(UIControlledApplication application)
        {
            // No cleanup needed for this plugin
            return Result.Succeeded;
        }

        /// <summary>
        /// Loads a PNG icon from resources. Tries pack URI first, then falls back to disk path in output Resources folder.
        /// </summary>
        private BitmapImage LoadPngIcon(string relativePath)
        {
            // 1) Try WPF resource pack URI
            try
            {
                var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
                var packUri = new Uri($"pack://application:,,,/{assemblyName};component/{relativePath}", UriKind.Absolute);
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = packUri;
                bi.EndInit();
                return bi;
            }
            catch
            {
                // ignored, fallback to disk below
            }

            // 2) Fallback: load from output folder (Resources copied as Content)
            var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            var diskPath = Path.Combine(baseDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(diskPath))
            {
                throw new FileNotFoundException($"Cannot locate resource '{relativePath}'. Tried: {diskPath}");
            }

            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(diskPath, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            return image;
        }
    }
}
