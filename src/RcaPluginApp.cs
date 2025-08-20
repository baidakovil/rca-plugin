using Autodesk.Revit.UI;
using RcaPlugin.Views;
using System;
using System.Reflection;

namespace RcaPlugin
{
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

                // Register dockable pane with parameterless provider
                var dpId = new DockablePaneId(new Guid(DockablePaneGuid));
                var provider = new RcaDockablePanelProvider();
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
    }
}
