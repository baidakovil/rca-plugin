# Revit Integration and Startup Process

This document provides a detailed explanation of how the hot-reloading system integrates with Revit's startup process and lifecycle.

## Revit Plugin Architecture Overview

Before diving into the startup process, it's important to understand the basic architecture of Revit plugins:

```
┌──────────────   ┐    ┌──────────────    ┐    ┌────    ──────────┐
│  Revit API      │◄───┤ RCA Loader       │◄───┤ RCA Runtime      │
│  (RevitAPI.dll) │    │ (Rca.Loader.dll) │    │ (Rca.Runtime.dll)│
└──────────────   ┘    └──────────    ────┘    └─────────────    ─┘
```

- **Revit API**: The core Autodesk Revit API that provides access to Revit's functionality
- **RCA Loader**: Fixed component that provides stability and bootstrapping
- **RCA Runtime**: Dynamic component that can be hot-reloaded without restarting Revit

## Revit Startup Sequence with Hot-Reloading System

### 1. Initial Plugin Loading

When Revit starts, it scans the addin directories and loads plugins based on their manifest files:

```
Revit starts
  ↓
Scan %APPDATA%\Autodesk\Revit\Addins\2026\
  ↓
Find and parse RcaLoader.addin
  ↓
Load Rca.Loader.dll
  ↓
Instantiate LoaderApp class
  ↓
Call LoaderApp.OnStartup()
```

### 2. LoaderApp Initialization

The `LoaderApp.OnStartup()` method performs critical initialization tasks:

```csharp
public Result OnStartup(UIControlledApplication application)
{
    try
    {
        // Initialize assembly status manager - critical for hot-reloading
        assemblyStatusManager = new AssemblyStatusManager();
        assemblyStatusManager.InitializeOnStartup();
        
        // Build the ribbon UI including reload button
        ribbonService.BuildRibbon(application);
        
        // Initialize status display if in DEBUG build
        UpdateStatusDisplay();
        
        return Result.Succeeded;
    }
    catch (Exception ex)
    {
        // Error handling
    }
}
```

### 3. Assembly Status Initialization

During initialization, the system checks for existing assemblies and their state:

```
AssemblyStatusManager.InitializeOnStartup()
  ↓
Check if LoadedAssemblies.json exists
  |
  ├── Yes → Load existing state
  |         Check if paths are valid
  |
  └── No → Create initial state
          Calculate hashes for current assemblies
          Save to new JSON file
```

### 4. Two-Phase Initialization Pattern

Revit's plugin initialization occurs in two distinct phases:

#### Phase 1: OnStartup
- Called during Revit's initial UI setup
- No document or active UIApplication available
- System initializes assembly tracking
- Builds ribbon UI and command buttons

#### Phase 2: External Command Initialization
- User triggers the hidden Initialize command (or any command)
- Command receives UIApplication reference
- `LoaderApp.InitializeWithUIApplication()` is called
- Pipe server is started for external communication

```
User action (automatic via ribbon load)
  ↓
InitializerCommand.Execute()
  ↓
LoaderApp.InitializeWithUIApplication(uiapp)
  ↓
StartPipeServer()
  ↓
Create RuntimeCommandHandler with access to AssemblyStatusManager
  ↓
System ready for hot-reloading commands
```

### 5. Runtime Loading Sequence

Once initialization is complete, the runtime is loaded on-demand:

```
First runtime-requiring operation
  ↓
RuntimeManager.EnsureRuntimeLoaded()
  ↓
Check if runtime already loaded
  |
  ├── Yes → Use existing runtime
  |
  └── No → Find latest runtime assembly
          Load assembly using Assembly.LoadFrom()
          Cache assembly reference
```

## JSON State Persistence and Revit Sessions

### Initial State Calculation

On first run, the system:
1. Identifies the executing Loader assembly location
2. Calculates hashes for Loader and Contracts assemblies
3. Attempts to find Runtime assembly in temporary folders
4. Creates initial JSON structure with paths and hashes

```json
{
  "LoaderComponents": {
    "Path": "C:\\Users\\...\\AppData\\Roaming\\Autodesk\\Revit\\Addins\\2026",
    "Hash": "a1b2c3d4e5f6..."
  },
  "RuntimeAssembly": {
    "Path": "C:\\Users\\...\\AppData\\Local\\RCA\\Runtime\\20231101-120000\\Rca.Runtime.dll",
    "Hash": "f6e5d4c3b2a1..."
  },
  "LastMSBuildSignal": {
    "Time": "",
    "Event": "no changes"
  }
}
```

### State Between Sessions

The JSON file persists between Revit sessions, allowing the system to:
1. Detect when newer versions are available
2. Understand which components have changed since last use
3. Track the history of MSBuild signals

## Hot-Reload during Revit Session

### User-Triggered Reload

When a user clicks the "Reload Runtime" button:

```
ReloadRuntimeCommand.Execute()
  ↓
Check if Loader is outdated via AssemblyStatusManager.IsLoaderOutdated()
  |
  ├── Yes → Show restart dialog with options
  |         User chooses action
  |         If restart chosen, execute restart script
  |
  └── No → Check if Runtime is outdated
            |
            ├── Yes → RuntimeManager.ReloadLatest()
            |         Update JSON with new hash
            |
            └── No → Show "Already up to date" message
```

### External Tool Integration

When triggered by an external build tool via named pipe:

```
External build tool creates new build
  ↓
Build process copies files to %LOCALAPPDATA%\RCA\Runtime\{timestamp}\
  ↓
Tool sends RELOAD_RUNTIME command via named pipe
  ↓
PipeServerService processes command
  ↓
RuntimeCommandHandler.HandleReloadRuntimeCommand()
  ↓
AssemblyStatusManager.ProcessMsBuildSignal()
  ↓
System determines what's changed
  ↓
If only Runtime is outdated, reload it and return success
```

## Revit Restart Process

When Loader components need to be updated:

```
RestartManager.ExecuteRestartScript()
  ↓
PowerShell script runs (RestartRevitGraceful.ps1)
  ↓
Close Revit gracefully
  ↓
Copy latest assemblies to Revit addin directory
  ↓
Update JSON state file with new paths and reset event
  ↓
Launch Revit executable
  ↓
Normal Revit startup process begins
  ↓
LoaderApp.OnStartup() loads JSON with updated info
```

## Thread Synchronization with Revit UI

Revit's UI runs on a single thread, and all Revit API calls must be made on that thread. The hot-reloading system handles this by:

1. Using `Dispatcher.Invoke()` for UI updates in RibbonStatusDisplay
2. Ensuring all Revit API operations are performed on the main thread
3. Leveraging Revit's `ExternalEvent` pattern for cross-thread communication (when needed)

## Diagnostic Capabilities

In DEBUG builds, the system provides real-time status information:

1. Status display in the Revit ribbon shows:
   - Current loaded assembly versions
   - Whether components are outdated
   - Last MSBuild signal information

2. Detailed logging to the Debug output:
   - Initialization sequence
   - Hash calculation results
   - State changes during operation
   - Errors and exception details

## Critical Sections and Error Handling

The hot-reloading system is designed to fail gracefully:

1. **JSON File Access**: 
   - If corrupted/missing, the system recreates it
   - Handles file access errors without crashing Revit

2. **Assembly Loading**:
   - Validates assemblies before attempting to load
   - Catches exceptions during assembly loading
   - Provides detailed error messages to users

3. **Runtime Reloading**:
   - Performs AppDomain isolation for clean reloading
   - Handles references to prevent memory leaks
   - Verifies type compatibility between versions

4. **Restart Process**:
   - Validates files before attempting restart
   - Logs detailed information about the restart process
   - Handles failed restarts without crashing

By understanding this integration and startup process, developers can effectively work with the hot-reloading system and troubleshoot any issues that arise.
