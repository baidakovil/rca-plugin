using System;
using Autodesk.Revit.UI;
using Rca.Loader.Logging;
using Microsoft.Extensions.Logging;

namespace Rca.Loader.Infrastructure
{
    /// <summary>
    /// Simple ExternalEvent handler that triggers a Revit command by ID.
    /// 
    /// Why this exists:
    /// - Named pipe commands arrive on background thread
    /// - Revit commands must be triggered on UI thread
    /// - ExternalEvent bridges this gap
    /// - Reuses existing command logic instead of duplicating dialogs
    /// </summary>
    internal class TriggerCommandHandler : IExternalEventHandler
    {
        private static readonly ILogger Log = LoaderLog.GetLogger<TriggerCommandHandler>();
        private readonly string commandName;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="TriggerCommandHandler"/> class.
        /// </summary>
        /// <param name="commandName">Name of the command to trigger (for logging).</param>
        public TriggerCommandHandler(string commandName)
        {
            this.commandName = commandName ?? "Unknown";
        }
        
        /// <summary>
        /// Executes the handler on Revit UI thread to trigger the command.
        /// 
        /// Implementation: Uses PostCommand to trigger ReloadRuntimeCommand.
        /// This invokes the ribbon button's command, which shows the user dialog.
        /// </summary>
        /// <param name="app">The Revit UI application.</param>
        public void Execute(UIApplication app)
        {
            try
            {
                Log.LogInformation("TriggerCommandHandler executing for command={Command}", commandName);
                
                // Get the command ID from ribbon
                // The ReloadRuntimeCommand is registered as "RCA_ReloadRuntime" button
                var commandId = RevitCommandId.LookupCommandId("CustomCtrl_%CustomCtrl_%RCA%Loader%RCA_ReloadRuntime");
                
                if (commandId != null)
                {
                    // Trigger the command
                    app.PostCommand(commandId);
                    Log.LogDebug("Command posted successfully commandId={Id}", commandId.Name);
                }
                else
                {
                    // Fallback: if command ID lookup fails, log warning
                    // User can still click the button manually
                    Log.LogWarning("Could not find command ID for {Command} - user must click button manually", commandName);
                }
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "Error executing TriggerCommandHandler for command={Command}", commandName);
            }
        }
        
        /// <summary>
        /// Gets the name of this external event handler.
        /// </summary>
        /// <returns>Handler name for debugging.</returns>
        public string GetName() => $"RCA Trigger {commandName}";
    }
}
