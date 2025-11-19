using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI;
using Rca.Loader.Testing;
using Rca.Loader.Contracts;
using Rca.Loader.Services;
using Rca.Loader.Infrastructure;
using Rca.Loader.AssemblyManagement;
using Rca.Loader.Logging;
using Microsoft.Extensions.Logging;

namespace Rca.Loader.Infrastructure
{
  /// <summary>
  /// Service for handling pipe commands that interact with the runtime.
  /// </summary>
  public class RuntimeCommandHandler
  {
    private static readonly ILogger Log = LoaderLog.GetLogger<RuntimeCommandHandler>();

    private readonly IRuntimeManager runtimeManager;
    private readonly UIApplication uiapp;
    private readonly CommandValidationService validationService;
    private readonly AssemblyStatusManager? assemblyStatusManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeCommandHandler"/> class.
    /// </summary>
    /// <param name="runtimeManager">The runtime manager.</param>
    /// <param name="uiapp">The Revit UI application.</param>
    public RuntimeCommandHandler(IRuntimeManager runtimeManager, UIApplication uiapp)
    {
      this.runtimeManager = runtimeManager ?? throw new ArgumentNullException(nameof(runtimeManager));
      this.uiapp = uiapp ?? throw new ArgumentNullException(nameof(uiapp));
      validationService = new CommandValidationService();
      assemblyStatusManager = LoaderApp.Instance?.AssemblyStatusManager;

      if (assemblyStatusManager == null)
      {
        Log.LogWarning("AssemblyStatusManager not available in RuntimeCommandHandler (hot-reload not fully initialized?)");
      }
    }

    /// <summary>
    /// Handles a pipe command asynchronously.
    /// </summary>
    /// <param name="cmd">The command to handle.</param>
    /// <returns>A response to the command.</returns>
    public async Task<PipeResponse> HandlePipeCommandAsync(PipeCommand cmd)
    {
      if (cmd == null) throw new ArgumentNullException(nameof(cmd));
      Log.LogDebug("Received pipe command {Command} payloadLen={Len}", cmd.Command, cmd.Payload?.Length ?? 0);

      try
      {
        // Validate command first
        if (!validationService.ValidateCommand(cmd, out var validationError))
        {
          Log.LogWarning("Command validation failed {Command} error={Error}", cmd.Command, validationError);
          return PipeResponseFactory.InvalidPayload(validationError);
        }

        return cmd.Command.ToUpperInvariant() switch
        {
          PipeCommands.RunTests => await HandleRunTestsCommandAsync(cmd).ConfigureAwait(false),
          PipeCommands.TestInit => await HandleTestInitCommandAsync().ConfigureAwait(false),
          _ => HandleSyncCommand(cmd)
        };
      }
      catch (Exception ex)
      {
        Log.LogError(ex, "Error handling command {Command}", cmd.Command);
        return PipeResponseFactory.Error($"Error handling command: {ex.Message}");
      }
    }

    /// <summary>
    /// Handles synchronous pipe commands.
    /// </summary>
    /// <param name="cmd">The command to handle.</param>
    /// <returns>A response to the command.</returns>
    private PipeResponse HandleSyncCommand(PipeCommand cmd)
    {
      Log.LogTrace("Handling synchronous command {Command}", cmd.Command);
      return cmd.Command.ToUpperInvariant() switch
      {
        PipeCommands.Reload => HandleReloadCommand(cmd),
        PipeCommands.ReloadRuntime => HandleReloadRuntimeCommand(cmd),
        PipeCommands.BuildCompleted => HandleBuildCompletedCommand(),
        PipeCommands.Status => HandleStatusCommand(),
        _ => PipeResponseFactory.UnknownCommand(cmd.Command)
      };
    }

    private PipeResponse HandleReloadCommand(PipeCommand cmd)
    {
      Log.LogInformation("Handling RELOAD command payload={Payload}", cmd.Payload);
      try
      {
        // Always force-unload any active test ALC for explicit RELOAD and let runtime reload fresh
        RevitTestExecutor.ForceUnloadActiveTestLoadContext();

        var result = runtimeManager.ReloadRuntime(cmd.Payload, out var errorMessage);
        if (result && !string.IsNullOrEmpty(cmd.Payload))
        {
          assemblyStatusManager?.ProcessMsBuildSignal(cmd.Payload);
          try { LoaderApp.Instance?.UpdateStatusDisplay(); } catch { }

          // Inject runtime UI into dockable panel host
          TryInjectRuntimeUI();

          Log.LogInformation("Runtime reloaded via explicit folder payload path={Path}", cmd.Payload);
        }
        return result ? PipeResponseFactory.Success(errorMessage ?? string.Empty) : PipeResponseFactory.Error(errorMessage ?? "Unknown reload error");
      }
      catch (Exception ex)
      {
        Log.LogError(ex, "Error in HandleReloadCommand payload={Payload}", cmd.Payload);
        return PipeResponseFactory.Error($"Error reloading: {ex.Message}");
      }
    }

    private PipeResponse HandleReloadRuntimeCommand(PipeCommand cmd)
    {
      Log.LogInformation("Handling RELOAD_RUNTIME command (auto-detect latest) payloadIgnoredLen={Len}", cmd.Payload?.Length ?? 0);
      try
      {
        // Always force-unload any active test ALC to clear stuck test references
        RevitTestExecutor.ForceUnloadActiveTestLoadContext();

        // Determine latest folder automatically
        var latest = assemblyStatusManager?.GetLatestTempDllFolder() ?? string.Empty;
        if (string.IsNullOrEmpty(latest))
          return PipeResponseFactory.Error("No runtime deploy folders found");

        // Update status manager from latest folder
        assemblyStatusManager?.ProcessMsBuildSignal(latest);
        // Refresh UI after processing MSBuild signal
        try { LoaderApp.Instance?.UpdateStatusDisplay(); } catch { }

        bool loaderOutdated = assemblyStatusManager?.IsLoaderOutdated() ?? false;
        bool runtimeOutdated = assemblyStatusManager?.IsRuntimeOutdated() ?? false;
        Log.LogDebug("ReloadRuntime state loaderOutdated={LoaderOutdated} runtimeOutdated={RuntimeOutdated}", loaderOutdated, runtimeOutdated);

        if (loaderOutdated && !runtimeOutdated)
          return PipeResponseFactory.Success("LOADER_RESTART_REQUIRED");
        if (!loaderOutdated && !runtimeOutdated)
          return PipeResponseFactory.Success("NO_ACTION_NEEDED");

        // Otherwise attempt runtime reload from latest
        var result = runtimeManager.ReloadRuntime(latest, out var errorMessage);
        if (result)
        {
          // Update runtime hash if reload was successful
          assemblyStatusManager?.UpdateHashesAfterReload(runtimeManager.CurrentRuntimePath);
          // Refresh UI to show updated hash/path
          try { LoaderApp.Instance?.UpdateStatusDisplay(); } catch { }

          // Inject runtime UI into dockable panel host
          TryInjectRuntimeUI();

          Log.LogInformation("ReloadRuntime completed (latest={Latest})", latest);
          return PipeResponseFactory.Success("ReloadRuntime completed successfully");
        }
        Log.LogWarning("ReloadRuntime failed latest={Latest} error={Error}", latest, errorMessage);
        return PipeResponseFactory.Error(errorMessage ?? "Unknown reload error");
      }
      catch (Exception ex)
      {
        Log.LogError(ex, "Error in HandleReloadRuntimeCommand");
        return PipeResponseFactory.Error($"Error in ReloadRuntime: {ex.Message}");
      }
    }

    /// <summary>
    /// Attempts to inject runtime UI into the dockable panel host after successful runtime load.
    /// </summary>
    private void TryInjectRuntimeUI()
    {
      try
      {
        var host = LoaderApp.Instance?.PanelHost;
        if (host == null)
        {
          Log.LogWarning("PanelHost unavailable for UI injection");
          return;
        }

        var content = runtimeManager.CreateRuntimeDockableContent(out var error);
        if (content != null)
        {
          host.SetContent(content);
          Log.LogInformation("Runtime UI injected successfully");
        }
        else
        {
          Log.LogWarning("Failed to create runtime UI: {Error}", error);
        }
      }
      catch (Exception ex)
      {
        Log.LogError(ex, "Error injecting runtime UI");
      }
    }

    private PipeResponse HandleStatusCommand()
    {
      Log.LogTrace("Handling STATUS command");
      try
      {
        var isRuntimeLoaded = runtimeManager.IsRuntimeLoaded;
        var path = isRuntimeLoaded ? runtimeManager.CurrentRuntimePath : string.Empty;
        Log.LogDebug("Status runtimeLoaded={Loaded} path={Path}", isRuntimeLoaded, path);
        return isRuntimeLoaded ? PipeResponseFactory.Loaded(path) : PipeResponseFactory.Empty();
      }
      catch (Exception ex)
      {
        Log.LogError(ex, "Error in HandleStatusCommand");
        return PipeResponseFactory.Error($"Error getting status: {ex.Message}");
      }
    }

    /// <summary>
    /// Handles BUILD_COMPLETED command from MSBuild.
    /// 
    /// This command is sent after a successful build to notify the running addin
    /// that new assemblies are available. The handler:
    /// 1. Finds the latest deploy folder automatically
    /// 2. Reads hashes from DLLs to detect what changed
    /// 3. Updates UI status display
    /// 4. Triggers ReloadRuntimeCommand to show dialog to user
    /// 
    /// Why trigger ReloadRuntimeCommand instead of showing dialog directly:
    /// - Reuses existing, well-tested dialog logic
    /// - Avoids code duplication
    /// - Ensures consistent UX across manual and automatic triggers
    /// </summary>
    /// <returns>Success response indicating command was processed.</returns>
    private PipeResponse HandleBuildCompletedCommand()
    {
      Log.LogInformation("Handling BUILD_COMPLETED notification from MSBuild");

      try
      {
        // 1. Find the latest deploy folder
        var latest = assemblyStatusManager?.GetLatestTempDllFolder() ?? string.Empty;
        if (string.IsNullOrEmpty(latest))
        {
          Log.LogWarning("No deploy folders found after build notification");
          return PipeResponseFactory.Success("NO_DEPLOY_FOUND");
        }

        Log.LogDebug("Latest deploy folder: {Folder}", latest);

        // 2. Process the build signal - updates CurrentInfo with new hashes
        assemblyStatusManager?.ProcessMsBuildSignal(latest);

        // 3. Update UI status display
        try { LoaderApp.Instance?.UpdateStatusDisplay(); } catch { }

        // 4. Trigger ReloadRuntimeCommand via ExternalEvent
        //    This reuses existing dialog logic from ReloadRuntimeCommand
        //    which already handles Loader outdated, Runtime outdated scenarios
        TriggerReloadRuntimeCommand();

        Log.LogInformation("BUILD_COMPLETED processed, ReloadRuntimeCommand triggered");
        return PipeResponseFactory.Success("ReloadRuntimeCommand triggered");
      }
      catch (Exception ex)
      {
        Log.LogError(ex, "Error handling BUILD_COMPLETED");
        return PipeResponseFactory.Error($"Error processing build notification: {ex.Message}");
      }
    }

    /// <summary>
    /// Triggers ReloadRuntimeCommand via ExternalEvent on UI thread.
    /// 
    /// Why this approach:
    /// - BUILD_COMPLETED arrives on background thread (named pipe)
    /// - ReloadRuntimeCommand contains all dialog logic we need
    /// - ExternalEvent is Revit API's mechanism for executing commands programmatically
    /// - Avoids code duplication of dialog logic
    /// </summary>
    private void TriggerReloadRuntimeCommand()
    {
      try
      {
        Log.LogDebug("Creating ExternalEvent to trigger ReloadRuntimeCommand");

        // Create a simple handler that invokes ReloadRuntimeCommand
        var handler = new TriggerCommandHandler("ReloadRuntimeCommand");
        var externalEvent = ExternalEvent.Create(handler);
        externalEvent.Raise();

        Log.LogDebug("ExternalEvent raised successfully");
      }
      catch (Exception ex)
      {
        Log.LogError(ex, "Error raising ExternalEvent for ReloadRuntimeCommand");
      }
    }

    private Task<PipeResponse> HandleTestInitCommandAsync()
    {
      Log.LogInformation("Handling TEST_INIT command");
      return Task.FromResult(PipeResponseFactory.Success("Test execution ready"));
    }

    private async Task<PipeResponse> HandleRunTestsCommandAsync(PipeCommand cmd)
    {
      Log.LogInformation("Handling RUN_TESTS command payloadLen={Len}", cmd.Payload?.Length ?? 0);
      if (string.IsNullOrEmpty(cmd.Payload))
      {
        Log.LogWarning("RUN_TESTS empty payload");
        return PipeResponseFactory.InvalidPayload("Empty test payload");
      }
      try
      {
        // Deserialize the test execution payload from the test adapter
        var payload = JsonSerializer.Deserialize<TestAdapterPayload>(cmd.Payload);
        if (payload == null)
        {
          Log.LogWarning("RUN_TESTS invalid payload format");
          return PipeResponseFactory.InvalidPayload("Invalid test payload format");
        }

        Log.LogDebug("Executing {Count} tests from assembly {Assembly}", payload.Tests.Count, payload.AssemblyPath);
        // Convert test adapter types to RevitTestExecutor types
        var executorRequests = payload.Tests.Select(test => new RevitTestExecutor.TestRequest
        {
          FullyQualifiedName = test.FullyQualifiedName,
          DisplayName = test.DisplayName
        }).ToList();

        // Create a test executor
        var testExecutor = new RevitTestExecutor(uiapp);

        // Execute the tests - this could be CPU intensive for large test suites,
        // so run it on a background thread to avoid blocking the UI
        var results = await Task.Run(() => testExecutor.ExecuteTests(payload.AssemblyPath, executorRequests)).ConfigureAwait(false);

        // Convert results back to test adapter format
        var adapterResults = results.Select(result => new TestAdapterResult
        {
          FullyQualifiedName = result.FullyQualifiedName,
          DisplayName = result.DisplayName,
          Outcome = result.Outcome,
          ErrorMessage = result.ErrorMessage,
          ErrorStackTrace = result.ErrorStackTrace,
          DurationInMilliseconds = result.DurationInMilliseconds,
          StartTimeUnixMs = result.StartTimeUnixMs,
          EndTimeUnixMs = result.EndTimeUnixMs,
          Messages = result.Messages.Select(msg => new TestAdapterMessage
          {
            Level = msg.Level,
            Text = msg.Text
          }).ToList()
        }).ToList();

        // Serialize the results
        var resultsJson = JsonSerializer.Serialize(adapterResults);
        Log.LogInformation("Test execution completed results={Count}", adapterResults.Count);
        return PipeResponseFactory.Success(resultsJson);
      }
      catch (JsonException ex)
      {
        Log.LogError(ex, "RUN_TESTS JSON serialization error");
        return PipeResponseFactory.Error($"JSON serialization error: {ex.Message}");
      }
      catch (Exception ex)
      {
        Log.LogError(ex, "RUN_TESTS execution error");
        return PipeResponseFactory.Error($"Test execution error: {ex.Message}");
      }
    }

    #region Test Adapter Data Transfer Objects

    /// <summary>
    /// Test execution payload from the test adapter.
    /// </summary>
    private class TestAdapterPayload
    {
      public string AssemblyPath { get; set; } = string.Empty;
      public List<TestAdapterRequest> Tests { get; set; } = new();
    }

    /// <summary>
    /// Test request from the test adapter.
    /// </summary>
    private class TestAdapterRequest
    {
      public string FullyQualifiedName { get; set; } = string.Empty;
      public string DisplayName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Test result for the test adapter.
    /// </summary>
    private class TestAdapterResult
    {
      public string FullyQualifiedName { get; set; } = string.Empty;
      public string DisplayName { get; set; } = string.Empty;
      public string Outcome { get; set; } = string.Empty;
      public string ErrorMessage { get; set; } = string.Empty;
      public string ErrorStackTrace { get; set; } = string.Empty;
      public double DurationInMilliseconds { get; set; }
      public long StartTimeUnixMs { get; set; }
      public long EndTimeUnixMs { get; set; }
      public List<TestAdapterMessage> Messages { get; set; } = new();
    }

    /// <summary>
    /// Test message for the test adapter.
    /// </summary>
    private class TestAdapterMessage
    {
      public string Level { get; set; } = "Informational";
      public string Text { get; set; } = string.Empty;
    }

    #endregion
  }
}
