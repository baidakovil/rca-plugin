# Hot-Reload Architecture for RCA Plugin

This document describes the hot-reload implementation that allows rebuilding and reloading the RCA plugin inside a running Revit 2026 session without restarting Revit.

## Architecture Overview

The hot-reload system uses a Loader pattern with the following components:

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Revit 2026    │    │  External Tools │    │   Developer     │
│                 │    │                 │    │                 │
│ ┌─────────────┐ │    │ ┌─────────────┐ │    │ ┌─────────────┐ │
│ │ Rca.Loader  │ │◄──►│ │ HotReload   │ │◄──►│ │ Build Tools │ │
│ │             │ │    │ │ Client      │ │    │ │ VS/VS Code  │ │
│ └─────────────┘ │    │ └─────────────┘ │    │ └─────────────┘ │
│       │         │    └─────────────────┘    └─────────────────┘
│       ▼         │              │                      │
│ ┌─────────────┐ │              │                      │
│ │ Rca.Runtime │ │              │                      │
│ │ (Collectible│ │              │                      │
│ │ Assembly)   │ │              │                      │
│ └─────────────┘ │              │                      │
└─────────────────┘              │                      │
                                 │                      │
                            NamedPipe               MSBuild
                          "rca.hotreload"          Automation
```

## Components

### 1. Rca.Loader.Contracts
- **Purpose**: Decoupling interface between Loader and Runtime
- **File**: `src/Rca.Loader.Contracts/IRcaRuntime.cs`
- **Key Interface**: `IRcaRuntime` with methods:
  - `Startup(UIControlledApplication)` - Initialize runtime
  - `Shutdown()` - Clean shutdown
  - `RunTest(string)` - Future test execution
  - `GetStatus()` - Diagnostics

### 2. Rca.Loader
- **Purpose**: Stable shim that loads/unloads runtime assemblies
- **Entry Point**: `LoaderApp.cs` (replaces `RcaPluginApp` in .addin file)
- **Key Components**:
  - `RuntimeHost.cs` - Manages AssemblyLoadContext lifecycle
  - `HotReloadServer.cs` - NamedPipe server for external commands
  - `Commands/ReloadCommand.cs` - Optional manual UI trigger

#### RuntimeHost Details
- Uses `AssemblyLoadContext(isCollectible: true)` for clean unloading
- Shadow copy pattern: copies runtime to temp directory before loading
- Excludes RevitAPI assemblies from custom resolution (uses default context)
- Tracks `UIControlledApplication` for reload scenarios

#### HotReloadServer Details
- NamedPipe name: `"rca.hotreload"`
- JSON-based protocol with commands:
  - `{"Command":"PING"}` - Health check
  - `{"Command":"RELOAD"}` - Trigger runtime reload
  - `{"Command":"STATUS"}` - Get runtime status
  - `{"Command":"RUN_TEST","Filter":"TestName"}` - Run specific test
- Uses ExternalEvent to marshal operations to Revit UI thread

### 3. Rca.Runtime
- **Purpose**: Aggregated runtime assembly with ILRepack
- **Key Components**:
  - `RcaRuntimeApp.cs` - Runtime implementation (extracted from `RcaPluginApp`)
  - `Commands/ShowDockablePanelCommand.cs` - UI commands for runtime
  - MSBuild targets for assembly merging and post-build triggers

#### RcaRuntimeApp Details
- Implements `IRcaRuntime` interface
- Contains all original plugin functionality from `RcaPluginApp`
- Manages dependency injection and service registration
- Includes cleanup hooks for proper shutdown (TODO: comprehensive cleanup)

#### ILRepack Integration
```xml
<Target Name="MergeAssemblies" AfterTargets="Build">
  <!-- Merges: Rca.Contracts, Rca.Core, Rca.UI, Rca.Network, Rca.Runtime -->
  <!-- Excludes: RevitAPI.dll, RevitAPIUI.dll -->
  <!-- Produces: Single Rca.Runtime.dll -->
</Target>
```

#### Post-Build Automation
```xml
<Target Name="TriggerHotReload" AfterTargets="MergeAssemblies">
  <!-- Sends {"Command":"RELOAD"} via PowerShell to NamedPipe -->
  <!-- Enables seamless developer experience -->
</Target>
```

### 4. Rca.HotReload.Client
- **Purpose**: Command-line tool for external reload triggering
- **Usage**:
  ```bash
  dotnet run --project tools/Rca.HotReload.Client -- --command RELOAD
  dotnet run --project tools/Rca.HotReload.Client -- --command STATUS  
  dotnet run --project tools/Rca.HotReload.Client -- --test MyTest.Method
  ```

## Developer Workflow

### Initial Setup
1. Start Revit 2026
2. Ensure `Rca.Loader.addin` is in Revit addins directory
3. Loader automatically loads initial runtime

### Hot-Reload Development
1. Make code changes in any of: `Rca.Contracts`, `Rca.Core`, `Rca.UI`, `Rca.Network`
2. Build the solution: `dotnet build src/Rca.Runtime`
3. Post-build automation triggers reload automatically
4. Changes are live in Revit without restart

### Manual Reload (Alternative)
```bash
# Using client tool
dotnet run --project tools/Rca.HotReload.Client -- -c RELOAD

# Using PowerShell directly
$pipe = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'rca.hotreload', 'InOut')
$pipe.Connect(1000)
$writer = New-Object System.IO.StreamWriter($pipe)
$writer.WriteLine('{"Command":"RELOAD"}')
$writer.Flush()
```

## NamedPipe Protocol

### Request Format
```json
{
  "Command": "PING|RELOAD|STATUS|RUN_TEST",
  "Filter": "optional test filter for RUN_TEST"
}
```

### Response Format
```json
{
  "Type": "STATUS|ERROR|TEST_RESULT",
  "State": "Connected|Reloading|Ready|Error",
  "Version": "1.0.0+20241215.153045",
  "Message": "error details if Type=ERROR"
}
```

## File Changes Summary

### New Files
- `src/Rca.Loader.Contracts/IRcaRuntime.cs`
- `src/Rca.Loader/LoaderApp.cs`
- `src/Rca.Loader/RuntimeHost.cs`
- `src/Rca.Loader/HotReloadServer.cs`
- `src/Rca.Loader/Commands/ReloadCommand.cs`
- `src/Rca.Runtime/RcaRuntimeApp.cs`
- `src/Rca.Runtime/Commands/ShowDockablePanelCommand.cs`
- `tools/Rca.HotReload.Client/Program.cs`
- `Rca.Loader.addin` (new entry point)

### Modified Files
- `rca-plugin.sln` - Added new projects
- `src/RcaPlugin/RcaPluginApp.cs` - Marked obsolete, kept for reference

## Limitations & Future Enhancements

### Current Limitations
1. **Event Cleanup**: Comprehensive cleanup of event subscriptions and ExternalEvent handlers not yet implemented
2. **Test Execution**: `RUN_TEST` command is stubbed (awaiting NUnitLite integration)
3. **WPF Resources**: Deep cleanup of WPF resource dictionaries on reload not implemented
4. **Memory Diagnostics**: Limited memory leak detection during reload cycles

### Future Enhancements (TODOs in code)
1. **Comprehensive Cleanup**: Implement thorough cleanup of:
   - Event subscriptions
   - ExternalEvent handlers  
   - WPF resources and themes
   - Background tasks/timers
2. **Test Runner**: Integrate NUnitLite for real `RUN_TEST` execution
3. **Diagnostics**: Add health commands:
   - `MEM_STATS` - Memory usage tracking
   - `LIST_EVENTS` - Active event handler inventory
4. **Security**: Pipe access control and command validation
5. **FileSystemWatcher**: Fallback reload mechanism when pipe unavailable
6. **Logging**: Structured logging forwarding over pipe protocol

## Troubleshooting

### Common Issues

**"Could not connect to reload server"**
- Ensure Revit is running with Loader loaded
- Check if NamedPipe `rca.hotreload` is available
- Verify no firewall blocking local pipe communication

**"Runtime assembly not found"**
- Ensure `Rca.Runtime.dll` is built and in Loader directory
- Check ILRepack merge completed successfully
- Verify all dependencies are available

**Memory leaks after multiple reloads**
- Current limitation - comprehensive cleanup not yet implemented  
- Restart Revit if memory usage becomes excessive
- Monitor with `STATUS` command for version tracking

### Debug Mode
To disable ILRepack for debugging:
1. Comment out `MergeAssemblies` target in `Rca.Runtime.csproj`
2. Ensure all individual DLLs are deployed alongside runtime
3. Re-enable merge target for production use

## Security Considerations

- NamedPipe currently has no access control (local machine only)
- Command validation is basic - expand for production use
- Consider authentication mechanism for external tools
- Audit code execution paths in runtime for security implications

---

This implementation provides a foundation for hot-reload development in Revit plugins while maintaining stability and backward compatibility.