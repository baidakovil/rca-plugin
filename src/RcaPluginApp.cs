using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Windows.Media.Imaging;
using System.Reflection;
using RcaPlugin.Views;

namespace RcaPlugin
{

    /// <summary>
    /// Actual UI application context for the Revit plugin.
    /// </summary>
    public static class RevitContext
    {
        public static UIApplication CurrentUIApplication { get; set; }
    }

    /// <summary>
    /// The main external application class for the RCA Plugin.
    /// </summary>
    public class RcaPluginApp : IExternalApplication
    {
        private const string DockablePaneGuid = "A1B2C3D4-E5F6-47A8-9B0C-1234567890AB";
        private const string DockablePaneName = "RCA Chat Assistant";
        private const string RibbonTabName = "RCA Plugin";
        private const string RibbonPanelName = "Chat Panel";
        private const string ButtonText = "Chat Assistant";

        private static UIApplication _uiapp;

        /// <summary>
        /// Called when Revit starts up.
        /// </summary>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Create ribbon tab and panel
                try { application.CreateRibbonTab(RibbonTabName); } catch { }
                var panel = application.CreateRibbonPanel(RibbonTabName, RibbonPanelName);

                // Create push button
                var buttonData = new PushButtonData(
                    "ShowChatPanel", 
                    ButtonText, 
                    Assembly.GetExecutingAssembly().Location, 
                    typeof(RcaPlugin.Commands.ShowDockablePanelCommand).FullName);
                panel.AddItem(buttonData);

                // Register dockable pane
                var dpId = new DockablePaneId(new Guid(DockablePaneGuid));
                // Provider will receive UIApplication later via static property
                var provider = new RcaDockablePanelProvider(null);
                application.RegisterDockablePane(dpId, DockablePaneName, provider);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Plugin Error", ex.Message);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Called when Revit shuts down.
        /// </summary>
        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        /// <summary>
        /// Used to set UIApplication for dockable panel context.
        /// </summary>
        public static void SetUIApplication(UIApplication uiapp)
        {
            _uiapp = uiapp;
        }

        /// <summary>
        /// Gets the current UIApplication instance.
        /// </summary>
        public static UIApplication GetUIApplication() => _uiapp;
    }
}
