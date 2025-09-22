# Hot-Reloading System Documentation

## Overview

The hot-reloading system in RCA Loader enables dynamic updates to both runtime and loader components without requiring a full manual restart of Revit. This document explains the components of the system, their responsibilities, and the flow of operations in different scenarios.

## System Components

### 1. Assembly Management

#### AssemblyInfo.cs

Represents information about a loaded assembly, tracking:
- **Path**: Location of the assembly on disk
- **Hash**: A SHA256 hash of the assembly content for change detection

#### LoadedAssembliesInfo.cs

Container class that holds:
- **LoaderComponents**: Represents the merged Loader assembly (includes Contracts)
- **RuntimeAssembly**: Information about the Runtime assembly
- **LastMSBuildSignal**: Tracks the most recent build notification

#### SignalInfo.cs

Records information about MSBuild signals:
- **Time**: Timestamp in HH:MM:SS format
- **Event**: Status string indicating what components are outdated

#### AssemblyStatusManager.cs

Core tracking service that:
- Monitors assembly versions via hash comparison
- Persists state between sessions using JSON
- Determines when assemblies need updating
- Provides APIs for checking outdated status

### 2. Visualization Components (DEBUG only)

#### RibbonStatusDisplay.cs

Displays real-time status of loaded assemblies:
- Shows which components are outdated
- Displays timestamp of last MSBuild signal
- Updates UI thread-safely using Dispatcher

#### RibbonService.cs (Modified)

Contains debug panel with:
- TextBox showing current assembly statuses
- Thread-safe update mechanism

### 3. Restart Mechanism

#### RestartManager.cs

Handles graceful restarts of Revit when Loader components change:
- Shows countdown dialog with options
- Executes PowerShell restart script
- Validates successful assembly copying

#### RestartRevitGraceful.ps1

PowerShell script that:
- Gracefully closes Revit with save prompts
- Copies updated merged Loader assembly to the target directory
- Updates state in JSON file
- Restarts Revit automatically

### 4. Command Integration

#### ReloadRuntimeCommand.cs (Enhanced)

Enhanced to:
- Check for outdated Loader components
- Present restart options to user when needed
- Update JSON state after successful reload

#### RuntimeCommandHandler.cs (Enhanced)

Handles pipe commands with:
- RELOAD_RUNTIME command for CI/CD integration
- Status reporting for different assembly states
- Comprehensive error logging

## Code Paths and Scenarios

### 1. Revit Startup Process

1. **Application Initialization**:
   ```
   LoaderApp.OnStartup()
     ↓
   AssemblyStatusManager.InitializeOnStartup()
     ↓
   LoadAssemblyInfo() or create initial state
   ```

2. **UI Setup in DEBUG Mode**:
   ```
   LoaderApp.OnStartup()
     ↓
   RibbonService.BuildRibbon()
     ↓
   Create Debug Panel with Status TextBox
     ↓
   Initialize RibbonStatusDisplay
     ↓
   Update with initial assembly status
   ```

3. **Pipe Server Initialization**:
   ```
   LoaderApp.InitializeWithUIApplication()
     ↓
   StartPipeServer()
     ↓
   Create RuntimeCommandHandler with AssemblyStatusManager
   ```

### 2. Runtime-Only Update Scenario

When only the Runtime assembly is updated:

1. **MSBuild Signal Detection**:
   ```
   RuntimeCommandHandler.HandleReloadRuntimeCommand()
     ↓
   AssemblyStatusManager.ProcessMsBuildSignal()
     ↓
   Calculate new hashes and compare with current
     ↓
   Determine "only runtime outdated" state
   ```

2. **User-Initiated Runtime Reload**:
   ```
   ReloadRuntimeCommand.Execute()
     ↓
   AssemblyStatusManager.IsRuntimeOutdated() == true
     ↓
   RuntimeManager.ReloadLatest()
     ↓
   AssemblyStatusManager.UpdateHashesAfterReload()
   ```

3. **IPC-Initiated Runtime Reload**:
   ```
   PipeServer receives RELOAD_RUNTIME command
     ↓
   RuntimeCommandHandler.HandleReloadRuntimeCommand()
     ↓
   RuntimeManager.ReloadRuntime()
     ↓
   AssemblyStatusManager.UpdateHashesAfterReload()
   ```

### 3. Loader Components Update Scenario

When the merged Loader assembly needs updating:

1. **Update Detection**:
   ```
   RuntimeCommandHandler.HandleReloadRuntimeCommand()
     ↓
   AssemblyStatusManager.ProcessMsBuildSignal()
     ↓
   AssemblyStatusManager.IsLoaderOutdated() == true
   ```

2. **User Interface Flow**:
   ```
   ReloadRuntimeCommand.Execute()
     ↓
   AssemblyStatusManager.IsLoaderOutdated() == true
     ↓
   Show TaskDialog with restart options
     ↓
   User chooses "Restart Revit"
     ↓
   RestartManager.ShowRestartDialog()
     ↓
   RestartManager.ExecuteRestartScript()
   ```

3. **Restart Execution**:
   ```
   RestartRevitGraceful.ps1 runs
     ↓
   Close Revit gracefully
     ↓
   Copy merged Loader assembly to target directory
     ↓
   Update JSON state file
     ↓
   Start new Revit process
   ```

### 4. Combined Updates Scenario

When both Loader and Runtime are updated:

1. **Update Detection**:
   ```
   RuntimeCommandHandler.HandleReloadRuntimeCommand()
     ↓
   AssemblyStatusManager.ProcessMsBuildSignal()
     ↓
   AssemblyStatusManager.IsLoaderOutdated() == true &&
   AssemblyStatusManager.IsRuntimeOutdated() == true
     ↓
   DetermineEventType() returns "both loader and runtime outdated"
   ```

2. **User Flow**:
   ```
   Same as Loader Components Update Scenario
   ```

3. **After Restart**:
   ```
   LoaderApp.OnStartup()
     ↓
   AssemblyStatusManager.InitializeOnStartup()
     ↓
   Load updated assembly info from JSON
     ↓
   AssemblyStatusManager.IsRuntimeOutdated() may still be true
   ```

## Implementation Notes

### Thread Safety Considerations

1. **UI Thread Access**:
   - All Revit API calls are made on the UI thread
   - RibbonStatusDisplay uses Dispatcher for thread-safe UI updates

2. **File Access**:
   - JSON operations are performed with appropriate error handling
   - Hash calculations handle file locks and access issues

### Configuration Persistence

1. **JSON State File**:
   - Located at `%LOCALAPPDATA%\RCA\LoadedAssemblies.json`
   - Contains paths and hashes for all tracked assemblies
   - Persists between Revit sessions

2. **Assembly Directory Structure**:
   - Runtime assemblies are stored in `%LOCALAPPDATA%\RCA\Runtime`
   - Merged Loader assembly is in the Revit addin directory (`%APPDATA%\Autodesk\Revit\Addins\2026`)

### Assembly Merging with ILRepack

1. **Merged Assembly Creation**:
   - Rca.Loader.dll and Rca.Loader.Contracts.dll are merged using ILRepack
   - MSBuild task handles RevitAPI dependencies properly during merge
   - Creates a single DLL that contains both components
   - Internalized types from Contracts aren't exposed outside Loader

2. **Build Process**:
   - Custom MSBuild task ensures proper merging even with RevitAPI dependencies
   - Enforces strict success/failure - no fallback to non-merged mode
   - Ensures consistent binaries across development and deployment

### Error Handling

1. **Graceful Degradation**:
   - System continues operating if status tracking fails
   - Comprehensive logging in DEBUG builds
   - User-friendly error messages via TaskDialog

2. **Recovery Mechanisms**:
   - Automatic detection of outdated components
   - Manual recovery option via ReloadRuntimeCommand

## Common Troubleshooting

1. **Missing Status Display**:
   - Only available in DEBUG builds
   - Check if TextBox was created successfully

2. **Failed Restart**:
   - Verify script path is correct
   - Ensure PowerShell execution policy allows script execution
   - Check logs at `%TEMP%\RcaRestartLog.txt`

3. **Hash Calculation Failures**:
   - Ensure files are not locked by other processes
   - Verify proper permissions to read files

4. **JSON File Corruption**:
   - Delete `%LOCALAPPDATA%\RCA\LoadedAssemblies.json` to reset state
   - System will recalculate hashes on next startup

5. **ILRepack Failures**:
   - Ensure RevitAPI.dll is available during build process
   - Check build logs for specific merge errors
   - Verify custom MSBuild task is working properly

## Best Practices for Development

1. **Testing Changes**:
   - Build directly to `%LOCALAPPDATA%\RCA\Runtime\{timestamp}` folder
   - Use ReloadRuntimeCommand to apply changes

2. **Debugging Restart Process**:
   - Add `-Verbose` flag to PowerShell script for detailed logging
   - Monitor `%TEMP%\RcaRestartLog.txt` for execution details

3. **Hot-Reload Limitations**:
   - Schema changes to public APIs require restart
   - Loader component changes always require restart
   - Runtime changes can be hot-reloaded

4. **CI/CD Integration**:
   - Use IPC with RELOAD_RUNTIME command from build scripts
   - Check response for "LOADER_RESTART_REQUIRED" signal
