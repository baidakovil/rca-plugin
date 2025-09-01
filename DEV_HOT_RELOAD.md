# RCA Plugin Hot Reload Development Guide

## Overview

The RCA Plugin now features a hot reload architecture that enables zero-restart development in Revit 2026. This system consists of:

- **Rca.Loader**: Stable add-in that never reloads
- **Rca.Runtime**: Dynamic runtime that contains the actual plugin logic
- **Named Pipe Protocol**: Communication between build system and loader

## Architecture

```
┌─────────────────┐    Named Pipe     ┌──────────────────┐
│   Build System  │ ◄──────────────► │   Rca.Loader     │
│   (MSBuild)     │    (JSON/IPC)     │   (Stable)       │
└─────────────────┘                   └──────────────────┘
         │                                       │
         │ Creates                               │ Loads/Unloads
         ▼                                       ▼
┌─────────────────┐                   ┌──────────────────┐
│  Staging Folder │                   │   Rca.Runtime    │
│  (Timestamped)  │                   │   (Dynamic)      │
│  + current.json │                   │   + All Logic    │
└─────────────────┘                   └──────────────────┘
```

## Components

### 1. Rca.Loader (Stable)
- Never unloads from Revit
- Manages AssemblyLoadContext for runtime
- Hosts Named Pipe server
- Provides manual reload ribbon button

### 2. Rca.Runtime (Dynamic)
- Contains all plugin business logic
- Implements IPluginRuntime interface
- Gets ILRepack-merged with dependencies
- Unloads cleanly for hot reload

### 3. Named Pipe Protocol
- **Pipe Name**: `rca.hotreload`
- **Commands**: RELOAD
- **Events**: RELOAD_START, RELOAD_DONE, RELOAD_FAIL, RUNTIME_ERROR, LOG

## Quick Start

### 1. Initial Setup
1. Build the solution to create all projects
2. Deploy Rca.Loader.addin to Revit addins folder
3. Start Revit - the loader will initialize

### 2. Development Workflow
```powershell
# Single command for hot reload development
dotnet build src/Rca.Runtime -c Debug
```

**That's it!** The build automatically:
1. Merges dependencies with ILRepack
2. Stages to timestamped folder
3. Updates `current.json` manifest
4. Notifies loader via named pipe
5. Loader unloads old runtime and loads new one

### 3. Manual Reload (if needed)
```powershell
# Option 1: Use PowerShell utility
pwsh -File tools/SendReload.ps1

# Option 2: Use Revit ribbon button
# Go to "RCA Loader" tab → "Hot Reload" panel → "Manual Reload"
```

## File Locations

### Staging Directory
```
%APPDATA%\RCA\LiveCore\
├── current.json                    # Current runtime manifest
├── build_20241201_143052\         # Timestamped runtime folders
│   └── Rca.Dynamic.dll            # Merged runtime assembly
├── build_20241201_143128\
│   └── Rca.Dynamic.dll
└── ...
```

### Manifest Format (current.json)
```json
{
  "folder": "C:\\Users\\...\\AppData\\Roaming\\RCA\\LiveCore\\build_20241201_143052",
  "assembly": "Rca.Dynamic.dll",
  "timestamp": "2024-12-01T14:30:52.123Z"
}
```

## Named Pipe Protocol

### Commands (Build → Loader)
```json
{
  "type": "COMMAND",
  "command": "RELOAD",
  "payload": {
    "folder": "optional_folder_path",
    "force": false
  },
  "timestamp": "2024-12-01T14:30:52.123Z"
}
```

### Events (Loader → Build)
```json
{
  "type": "EVENT", 
  "event": "RELOAD_DONE",
  "data": {
    "version": "1.0.0.0-abc12345"
  },
  "timestamp": "2024-12-01T14:30:52.123Z"
}
```

## Troubleshooting

### Build Issues
```powershell
# Clean rebuild if facing issues
dotnet clean
dotnet restore  
dotnet build src/Rca.Runtime -c Debug
```

### Memory Growth
- Expected: Some memory growth after reloads (acceptable)
- Problematic: Unbounded growth after 10+ reloads
- Debug: Check for static references preventing GC

### Assembly Load Context Collection
In DEBUG builds, check for `ALC_COLLECTED` events indicating successful context cleanup.

### Pipe Connection Issues
```powershell
# Check if loader is running
Get-Process | Where-Object {$_.ProcessName -like "*Revit*"}

# Test manual reload
pwsh -File tools/SendReload.ps1
```

### No Auto-Reload After Build
1. Check MSBuild output for pipe notification success
2. Verify current.json was updated
3. Try manual reload to isolate issue

## Advanced Usage

### Custom Build Scenarios
```xml
<!-- Custom MSBuild target -->
<Target Name="CustomHotReload" AfterTargets="AfterBuild">
  <!-- Your custom logic here -->
  <CallTarget Targets="HotReloadStaging" />
</Target>
```

### Multiple Runtime Versions
Each build creates a timestamped folder, allowing rollback:
```powershell
# Reload specific version
pwsh -File tools/SendReload.ps1 -Folder "C:\...\build_20241201_143052"
```

## AI Agent (GitHub Copilot) Usage

### Single Build Command
When making changes, use this single command:
```powershell
dotnet build src/Rca.Runtime -c Debug
```

### Test Scenario Workflow
1. Modify code in any referenced project (Core, UI, Network, etc.)
2. Run the build command above
3. Test changes immediately in Revit - no restart needed

### Common Patterns
- **UI Changes**: Modify XAML/ViewModels → build → see changes instantly
- **Business Logic**: Update Core services → build → test with Python panel
- **Commands**: Add new Revit commands → build → new buttons appear

## Performance Notes

- **Cold Start**: Initial load ~2-3 seconds
- **Hot Reload**: Subsequent reloads ~1-2 seconds  
- **Memory**: Some growth expected, should stabilize after few reloads
- **Dependencies**: Only project dependencies are merged, Revit APIs excluded

## Limitations

- **Windows only** (Named Pipes, Revit)
- **.NET 8 only** (collectible AssemblyLoadContext)
- **Revit 2026+** (net8.0-windows support)
- **Linux CI**: Projects compile syntax only, runtime requires Windows + Revit
- Some static state may persist between reloads

## Support

For issues or questions:
1. Check this documentation
2. Review MSBuild output for errors
3. Check Windows Event Log for pipe/assembly loading issues
4. Use DEBUG builds for additional logging