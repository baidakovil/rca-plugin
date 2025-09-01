# RCA Plugin Hot Reload Development Guide

This document explains how to use the hot reload functionality in the RCA Plugin for faster development without restarting Revit.

## Architecture Overview

The hot reload system consists of three main components:

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Rca.Loader    │    │  Named Pipe IPC  │    │  Rca.Runtime    │
│   (Stable)      │◄──►│   rca.hotreload  │◄──►│   (Dynamic)     │
│                 │    │                  │    │                 │
│ - LoaderApp     │    │ - RELOAD cmd     │    │ - RcaRuntime    │
│ - RuntimeManager│    │ - Events         │    │ - Business Logic│
│ - PipeServer    │    │ - Error handling │    │ - UI Components │
└─────────────────┘    └──────────────────┘    └─────────────────┘
         │                                              │
         │                                              │
    ┌────▼────┐                                   ┌─────▼─────┐
    │ Revit   │                                   │MSBuild    │
    │ Add-in  │                                   │ILRepack   │
    │ System  │                                   │Auto-Deploy│
    └─────────┘                                   └───────────┘
```

### Components

1. **Rca.Loader** - Stable loader that never gets reloaded
   - Manages AssemblyLoadContext lifecycle
   - Hosts Named Pipe server for build communication
   - Provides manual reload ribbon button

2. **Rca.Runtime** - Dynamic runtime that contains the business logic
   - Implements IPluginRuntime interface
   - Contains all UI, Core, Network functionality
   - Gets hot reloaded automatically after builds

3. **Rca.Loader.Contracts** - Shared interfaces and DTOs
   - IPluginRuntime interface
   - Named pipe protocol definitions
   - Constants and message types

## Quick Start

### 1. Initial Setup

The hot reload system is automatically configured. Just build the solution:

```powershell
# Build entire solution (this sets up the loader)
dotnet build

# Or build specific projects
dotnet build src/Rca.Loader      # Deploys stable loader to Revit
dotnet build src/Rca.Runtime     # Creates hot reload package
```

### 2. Start Revit

1. Open Revit 2026
2. The **Rca.Loader** will automatically start
3. Look for the "RCA Loader" ribbon tab with "Reload Runtime" button
4. The loader will attempt to load the latest runtime automatically

### 3. Development Workflow

The magic happens when you modify code and rebuild:

```powershell
# Make changes to any runtime code (UI, Core, Network, etc.)
# Then simply build the runtime project:
dotnet build src/Rca.Runtime

# This automatically:
# 1. Merges all dependencies with ILRepack
# 2. Creates timestamped deployment folder
# 3. Updates current.json manifest  
# 4. Sends RELOAD command via named pipe
# 5. Loader unloads old runtime and loads new one
```

**Result**: Your changes appear in Revit immediately without restart!

## Manual Operations

### Manual Reload Command

If automatic reload fails, use the manual button:

1. In Revit, go to **RCA Loader** ribbon tab
2. Click **Reload Runtime** button
3. This triggers the same reload process manually

### PowerShell Reload Client

For advanced scenarios, use the PowerShell client:

```powershell
# Reload from latest manifest
pwsh -File tools/SendReload.ps1

# Reload from specific folder
pwsh -File tools/SendReload.ps1 -Folder "C:\Users\...\RCA\LiveCore\build_20241210_143022"
```

## File Locations

### Runtime Staging
Hot reload packages are created in:
```
%LOCALAPPDATA%\RCA\LiveCore\
├── current.json                 # Points to active runtime
├── build_20241210_143022\       # Timestamped runtime folders
│   └── Rca.Dynamic.dll         # Merged runtime assembly
├── build_20241210_143108\
│   └── Rca.Dynamic.dll
└── ...
```

### Revit Add-ins
The stable loader is deployed to:
```
%APPDATA%\Autodesk\Revit\Addins\2026\
├── Rca.Loader.addin            # Loader manifest
└── Rca.Loader\                 # Loader assemblies
    ├── Rca.Loader.dll
    └── Rca.Loader.Contracts.dll
```

## Troubleshooting

### Build Issues

**Problem**: `ILRepack` fails during build
```
Solution: Ensure all referenced projects build successfully first
dotnet build src/Rca.Contracts
dotnet build src/Rca.Core  
dotnet build src/Rca.UI
dotnet build src/Rca.Network
dotnet build src/Rca.Runtime
```

**Problem**: Named pipe connection fails
```
Solution: Check that Revit is running and loader is active
- Look for "RCA Loader" ribbon tab in Revit
- Check Windows Event Viewer for pipe errors
- Try manual reload button first
```

### Memory Issues

**Problem**: Memory grows after multiple reloads
```
This is normal for up to ~10 reloads. The old AssemblyLoadContext instances
should be garbage collected. In DEBUG builds, check the Output window for
"ALC_COLLECTED" messages indicating successful cleanup.

Solution: If memory grows excessively, restart Revit periodically.
```

**Problem**: Assembly not unloading properly
```
Possible causes:
- Static references holding onto old assembly
- Event handlers not properly unsubscribed
- Long-lived objects preventing GC

Solution: Review static fields and event subscriptions in runtime code.
```

### Development Issues

**Problem**: Changes not reflected after reload
```
Check:
1. Did the build complete successfully?
2. Was the pipe notification sent? (check build output)
3. Is the correct version loading? (check debug output)
4. Try manual reload button

Solution: Check build output for ILRepack and pipe notification messages.
```

**Problem**: Revit crashes during reload
```
This usually indicates:
- Exception in runtime initialization code
- Incompatible assembly dependencies
- Threading issues in hot reload

Solution: Check exception details in Windows Event Viewer.
Enable DEBUG build for more detailed logging.
```

## Developer Tips

### For AI Agents (GitHub Copilot)

1. **Single Command Development**: Just run `dotnet build src/Rca.Runtime` to see changes
2. **Test Scenarios**: Make small changes (e.g., button text) and rebuild to verify hot reload
3. **Debugging**: Use `System.Diagnostics.Debug.WriteLine()` for runtime logging
4. **Version Checking**: The runtime version is shown in debug output after each reload

### Best Practices

1. **Keep Runtime Logic Stateless**: Avoid static fields that persist across reloads
2. **Proper Disposal**: Implement proper cleanup in `RcaRuntime.Shutdown()`
3. **Exception Handling**: Wrap initialization code in try-catch blocks
4. **Small Changes**: Test hot reload with small, isolated changes first

### Performance Notes

- **Reload Time**: Typically 2-5 seconds for full reload
- **Memory Usage**: Each reload creates ~10-50MB of temporary memory
- **Build Time**: ILRepack adds ~1-2 seconds to build time

## Named Pipe Protocol

### Commands (Client → Server)
```json
{"type": "RELOAD", "payload": {"folder": "optional_path"}}
```

### Events (Server → Client)
```json
{"type": "RELOAD_ACCEPTED", "payload": null, "timestamp": "2024-12-10T14:30:22Z"}
{"type": "RELOAD_START", "payload": null, "timestamp": "2024-12-10T14:30:22Z"}
{"type": "RELOAD_DONE", "payload": null, "timestamp": "2024-12-10T14:30:25Z"}
{"type": "RELOAD_FAIL", "payload": {"message": "Error details"}, "timestamp": "..."}
{"type": "RUNTIME_ERROR", "payload": {"message": "Runtime error"}, "timestamp": "..."}
```

## Integration with VS Code / Visual Studio

### VS Code Tasks (optional)

Create `.vscode/tasks.json`:
```json
{
    "version": "2.0.0",
    "tasks": [
        {
            "label": "Hot Reload RCA",
            "type": "shell",
            "command": "dotnet",
            "args": ["build", "src/Rca.Runtime"],
            "group": "build",
            "presentation": {
                "echo": true,
                "reveal": "always",
                "focus": false,
                "panel": "shared"
            }
        }
    ]
}
```

Use **Ctrl+Shift+P** → "Tasks: Run Task" → "Hot Reload RCA"

### Visual Studio

Create a custom build configuration that only builds `Rca.Runtime` project for fastest reload iteration.

## Conclusion

The hot reload system enables rapid development by eliminating Revit restart cycles. The typical workflow becomes:

1. **Code** → 2. **Build** → 3. **Test** (repeat)

Instead of:

1. **Code** → 2. **Build** → 3. **Close Revit** → 4. **Restart Revit** → 5. **Reload Project** → 6. **Test** (repeat)

This can save 30-60 seconds per iteration, dramatically improving development productivity.