# RCA Plugin Hot Reload Development Guide

This document describes the hot reload architecture for the RCA Plugin, enabling iterative development without restarting Revit.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                            Revit 2026 (.NET 8)                         │
├─────────────────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐    ┌───────────────────────────────────────────┐   │
│  │  Rca.Loader     │    │  HotReloadAssemblyLoadContext             │   │
│  │  (Stable)       │◄──►│  ┌─────────────────────────────────────┐  │   │
│  │                 │    │  │  Rca.Dynamic.dll (ILRepacked)       │  │   │
│  │ - LoaderApp     │    │  │  ┌─────────────────────────────────┐│  │   │
│  │ - RuntimeManager│    │  │  │  RcaRuntime : IPluginRuntime   ││  │   │
│  │ - PipeServer    │    │  │  │  - Core Business Logic         ││  │   │
│  │ - Manual Reload │    │  │  │  - UI Registration             ││  │   │
│  └─────────────────┘    │  │  │  - Service Container Setup    ││  │   │
│           ▲              │  │  └─────────────────────────────────┘│  │   │
│           │              │  └─────────────────────────────────────┘  │   │
│           │              └───────────────────────────────────────────┘   │
└───────────┼──────────────────────────────────────────────────────────────┘
            │ Named Pipe                                                     
            │ (JSON Commands)                                                
┌───────────▼──────────────────────────────────────────────────────────────┐
│                         Build System                                    │
├─────────────────────────────────────────────────────────────────────────┤
│  dotnet build src/Rca.Runtime                                           │
│  │                                                                      │
│  ├─► ILRepack (merge assemblies)                                        │
│  ├─► Stage to %LOCALAPPDATA%\RCA\LiveCore\build_YYYYMMDD_HHMMSS\        │
│  ├─► Update current.json manifest                                       │
│  └─► Send RELOAD command via pipe                                       │
└─────────────────────────────────────────────────────────────────────────┘
```

## Quick Start

### 1. Initial Setup
1. Build the entire solution to create the stable Loader:
   ```bash
   dotnet build rca-plugin.sln
   ```

2. Start Revit 2026. The Loader will be automatically deployed and started.

3. You should see:
   - "RCA Loader" tab in the Revit ribbon with a "Reload Runtime" button
   - A message indicating no runtime is available (first time)

### 2. Enable Hot Reload
Build the runtime project to create the initial hot-reloadable assembly:
```bash
dotnet build src/Rca.Runtime -c Debug
```

After the build completes:
- The runtime will be automatically staged and loaded
- The RCA Plugin UI should become available
- Console output will show staging and pipe notification status

### 3. Hot Reload Development
For iterative development, simply rebuild the runtime:
```bash
dotnet build src/Rca.Runtime -c Debug
```

**No Revit restart required!** Changes will be automatically reflected in the running plugin.

## Manual Reload Options

### Ribbon Button
Use the "Reload Runtime" button in the "RCA Loader" ribbon tab for manual reload control.

### PowerShell Script
```bash
# Reload using current manifest
pwsh -File tools/SendReload.ps1

# Reload specific build folder
pwsh -File tools/SendReload.ps1 -Folder build_20241215_143022
```

## Project Structure

### Core Projects
- **Rca.Loader.Contracts** - Interfaces and DTOs for hot reload protocol
- **Rca.Loader** - Stable loader (never unloaded), manages AssemblyLoadContext
- **Rca.Runtime** - Hot-reloadable runtime containing business logic
- **Rca.Contracts**, **Rca.Core**, **Rca.UI**, **Rca.Network** - Existing feature modules

### Key Files
- `%LOCALAPPDATA%\RCA\LiveCore\current.json` - Runtime manifest
- `%LOCALAPPDATA%\RCA\LiveCore\build_*\Rca.Dynamic.dll` - Staged runtime assemblies
- `%APPDATA%\Autodesk\Revit\Addins\2026\Rca.Loader.addin` - Stable loader registration

## Named Pipe Protocol

### Commands (Client → Loader)
```json
{
  "type": "RELOAD",
  "payload": {
    "folder": "build_20241215_143022"  // optional
  }
}
```

### Events (Loader → Client)
```json
{
  "type": "RELOAD_START|RELOAD_DONE|RELOAD_FAIL|RUNTIME_ERROR|LOG",
  "payload": { /* event-specific data */ },
  "timestamp": "2024-12-15T14:30:22.123Z"
}
```

## Troubleshooting

### Memory Growth
Monitor memory usage with multiple reloads:
- Some growth is expected but should stabilize
- Large continuous growth indicates assembly leak
- Debug builds show ALC collection status in console

### Assembly Unload Verification
In DEBUG builds, watch for "AssemblyLoadContext successfully collected" messages indicating proper cleanup.

### Common Issues

**"No runtime manifest found"**
- Build `src/Rca.Runtime` project first
- Check `%LOCALAPPDATA%\RCA\LiveCore\current.json` exists

**"Pipe notification failed"**
- Normal if Revit isn't running
- Check Windows named pipe availability
- Use manual reload button as fallback

**"Failed to reload runtime"**
- Check console output for detailed error messages
- Verify assembly dependencies are available
- Try manual reload command for testing

### Stale Static State
Some static state may persist between reloads. Design runtime logic to be stateless or implement reset patterns.

## AI Agent Guidance

For AI-assisted development (GitHub Copilot):

1. **Single Build Command**: Use `dotnet build src/Rca.Runtime` to trigger hot reload
2. **Test Scenarios**: Modify code → build → observe changes in running Revit
3. **No Restart Needed**: All changes apply immediately via hot reload
4. **Error Recovery**: Use manual reload button if automatic reload fails
5. **Development Flow**: Edit code → build → test → repeat (no Revit restarts)

## Implementation Details

### AssemblyLoadContext Management
- Uses `AssemblyLoadContext(isCollectible: true)` for true assembly unloading
- Double garbage collection pass ensures proper cleanup
- WeakReference tracking for memory leak detection

### ILRepack Integration
- Merges Rca.Runtime + dependencies into single `Rca.Dynamic.dll`
- Excludes RevitAPI, System.*, Microsoft.* assemblies
- Preserves debug information for development builds

### Atomic Manifest Updates
- Writes to temporary file then moves for atomic updates
- Prevents race conditions between build and loader
- Timestamped folders ensure version isolation

### Exception Handling
- Runtime errors captured and sent via pipe protocol
- Loader remains stable even if runtime fails
- Comprehensive error logging for diagnostics

## Future Enhancements
- Automated integration test harness
- VS Code tasks for single-command workflow  
- Real-time log streaming to IDE
- Cross-version Revit compatibility
- Advanced memory profiling integration