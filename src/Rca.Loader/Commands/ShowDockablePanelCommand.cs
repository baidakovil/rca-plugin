using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Rca.Loader.Logging;
using Microsoft.Extensions.Logging;

namespace Rca.Loader.Commands
{
  /// <summary>
  /// External command to show the Revit Chat Assistant dockable panel.
  /// If the panel is already visible, this command brings it to focus.
  /// </summary>
  [Transaction(TransactionMode.ReadOnly)]
  public class ShowDockablePanelCommand : IExternalCommand
  {
    private static readonly ILogger Log = LoaderLog.GetLogger<ShowDockablePanelCommand>();

    /// <summary>
    /// The dockable pane id - must match the GUID used during panel registration in LoaderApp.
    /// </summary>
    private static readonly DockablePaneId PaneId = new DockablePaneId(
        new Guid("3D5A1C2B-4F8E-4D3F-AF1E-1234567890AB")
    );

    /// <summary>
    /// Executes the command to show the Revit Chat Assistant dockable panel.
    /// </summary>
    /// <param name="commandData">The command data.</param>
    /// <param name="message">Error message.</param>
    /// <param name="elements">Elements for errors.</param>
    /// <returns>Result of the command.</returns>
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
      try
      {
        if (commandData == null)
        {
          message = "Command data is null";
          Log.LogWarning("Execute aborted: commandData is null");
          return Result.Failed;
        }

        UIApplication uiApp = commandData.Application;

        // Find the registered dockable pane by its ID
        DockablePane pane = uiApp.GetDockablePane(PaneId);
        if (pane == null)
        {
          message = "Dockable panel not found";
          TaskDialog.Show("RCA Loader Error",
              "Could not find the Revit Chat Assistant panel. Please restart Revit.");
          Log.LogWarning("Dockable pane not found with ID={PaneId}", PaneId.Guid);
          return Result.Failed;
        }

        // Show the panel (if already shown, brings it to focus)
        if (!pane.IsShown())
        {
          pane.Show();
          Log.LogInformation("Revit Chat Assistant panel shown");
        }
        else
        {
          Log.LogDebug("Revit Chat Assistant panel already visible");
        }

        return Result.Succeeded;
      }
      catch (Exception ex)
      {
        message = ex.Message;
        Log.LogError(ex, "Error showing Revit Chat Assistant panel");
        TaskDialog.Show("RCA Loader Error", $"Error showing panel: {ex.Message}");
        return Result.Failed;
      }
    }
  }
}
