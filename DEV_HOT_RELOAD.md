# Hot Reload Development Guide

This document describes the hot reload system for the RCA Plugin, enabling zero-restart development in Revit 2026.

## Architecture Overview

```
┌─────────────────┐    Named Pipe     ┌──────────────────┐
│   MSBuild       │◄──────────────────►│  Rca.Loader      │
│   (ILRepack +   │   JSON Messages    │  (Stable)        │
│    Pipe Client) │                    │                  │
└─────────────────┘                    └──────────────────┘
         │                                       │
         │ Creates merged                        │ Loads/Unloads
         │ Rca.Dynamic.dll                       │
         ▼                                       ▼
┌─────────────────┐                    ┌──────────────────┐
│ %APPDATA%\RCA\  │                    │ AssemblyLoad     │
│ LiveCore\       │                    │ Context          │
│ ├─current.json  │                    │ (Collectible)    │
│ └─build_xxx\    │                    │                  │
│   └─Rca.Dynamic │                    │                  │
└─────────────────┘                    └──────────────────┘
                                                │
                                                │ Implements
                                                ▼
                                       ┌──────────────────┐
                                       │  Rca.Runtime     │
                                       │  (Hot Reloadable)│
                                       │  • UI panels     │
                                       │  • Commands      │
                                       │  • Services      │
                                       └──────────────────┘
```

## Quick Start

### 1. Initial Setup
Ensure Revit 2026 is installed and the solution builds:

```powershell
# Build the entire solution (includes Loader + Runtime)
dotnet build

# Or build just the runtime for hot reload
dotnet build src/Rca.Runtime -c Debug
```

### 2. Launch Revit with Loader
1. Start Revit 2026
2. The Rca.Loader should load automatically (via .addin file)
3. Look for "RCA Loader" ribbon tab with "Reload Runtime" button
4. The initial runtime should load automatically if available

### 3. Hot Reload Development Cycle
```powershell
# Make changes to code in Rca.Runtime, Rca.Core, Rca.UI, etc.

# Build the runtime project (triggers hot reload)
dotnet build src/Rca.Runtime -c Debug

# Changes should appear immediately in Revit without restart!
```

## Manual Reload

If automatic reload fails, use the manual options:

### Option 1: Ribbon Button
Click "Reload Runtime" in the "RCA Loader" ribbon tab in Revit.

### Option 2: PowerShell Script
```powershell
# Reload from manifest
pwsh -File tools/SendReload.ps1

# Reload from specific folder
pwsh -File tools/SendReload.ps1 -Folder "C:\Users\...\AppData\Local\RCA\LiveCore\build_20231201_143022"

# Force reload even if version is same
pwsh -File tools/SendReload.ps1 -Force
```

## How It Works

### 1. Stable Loader (Rca.Loader)
- Registered in Revit via .addin file
- Never unloads - handles the hot reload lifecycle
- Manages AssemblyLoadContext for collectible loading
- Runs Named Pipe server for IPC

### 2. Dynamic Runtime (Rca.Runtime) 
- Contains all business logic from original RcaPluginApp
- Gets ILRepacked with dependencies into single Rca.Dynamic.dll
- Loaded into collectible AssemblyLoadContext
- Implements IPluginRuntime interface

### 3. Build Integration
When you build Rca.Runtime:
1. MSBuild Target "HotReloadDeploy" runs after build
2. ILRepack merges Rca.Runtime + dependencies → Rca.Dynamic.dll
3. Assembly copied to timestamped folder in %APPDATA%\RCA\LiveCore\
4. Manifest file (current.json) updated atomically
5. Reload command sent via Named Pipe to Loader
6. Loader unloads old context, loads new assembly

### 4. Named Pipe Protocol
JSON messages over pipe named "rca.hotreload":

**Command (Build → Loader):**
```json
{
  "Type": "COMMAND",
  "Payload": "{\"Command\":\"RELOAD\",\"Folder\":\"path\\to\\build\"}",
  "Timestamp": "2023-12-01T14:30:22.123Z"
}
```

**Events (Loader → Client):**
```json
{
  "Type": "EVENT", 
  "Event": "RELOAD_ACCEPTED|RELOAD_START|RELOAD_DONE|RELOAD_FAIL",
  "Payload": "details"
}
```

## File Locations

### Staging Directory
```
%APPDATA%\RCA\LiveCore\
├── current.json          # Points to active build
├── build_20231201_143022\ # Timestamped builds
│   └── Rca.Dynamic.dll
├── build_20231201_143156\
│   └── Rca.Dynamic.dll
└── ...
```

### Revit Add-in Directory
```
%APPDATA%\Autodesk\Revit\Addins\2026\
├── Rca.Loader.addin      # Points to stable Loader
└── Rca.Loader\           # Stable loader assemblies
    ├── Rca.Loader.dll
    ├── Rca.Loader.Contracts.dll
    └── ...
```

## Troubleshooting

### No Hot Reload Happening
1. Check if Revit is running and Loader is loaded
2. Verify pipe server is running: `pwsh -File tools/SendReload.ps1`
3. Check Visual Studio Output window for MSBuild messages
4. Look for errors in staging directory or manifest file

### Memory Issues After Many Reloads
- Some memory growth is expected (Revit API types may hold references)
- GC should collect old AssemblyLoadContext after unload
- In DEBUG builds, watch for "AssemblyLoadContext successfully collected" messages
- If memory grows too much, restart Revit

### Build Errors
```powershell
# Clean and rebuild if dependencies get out of sync
dotnet clean
dotnet restore  
dotnet build
```

### Stale Static Fields
- Static fields in reloaded assemblies get new instances
- Services are re-registered on each reload
- If you have persistent state, consider moving to Loader project

### ILRepack Issues
- Ensure all project dependencies are explicitly referenced
- Check for conflicting assembly versions
- RevitAPI assemblies are excluded from merge (Private=False)

## AI Agent Guidance (GitHub Copilot)

### Single Command Development
The main development command is:
```powershell
dotnet build src/Rca.Runtime -c Debug
```

This triggers the complete hot reload cycle. No other commands needed for typical development.

### Testing Scenarios
To test a specific change:
1. Modify code in Rca.Runtime, Rca.Core, Rca.UI, etc.
2. Run: `dotnet build src/Rca.Runtime -c Debug`
3. Observe change in Revit immediately
4. Use `tools/SendReload.ps1` if automatic reload fails

### Common Development Patterns
- **UI Changes**: Modify Rca.UI views/viewmodels → build → see changes
- **Business Logic**: Modify Rca.Core services → build → test functionality  
- **Commands**: Modify Rca.Runtime commands → build → test ribbon buttons
- **New Features**: Add to any project → ensure referenced by Rca.Runtime → build

### Debugging Tips
- Use `System.Diagnostics.Debug.WriteLine()` for logging (appears in VS Output)
- The Loader logs to Debug output with "[RuntimeManager]" prefix
- Runtime logs with "[RcaRuntime]" prefix
- Check %APPDATA%\RCA\LiveCore\ for build artifacts and manifest

### Performance Considerations
- Hot reload cycle typically takes 2-5 seconds
- ILRepack step is the slowest part
- Named pipe communication is near-instant
- Assembly loading depends on size of merged DLL

### Limitations
- Cannot change Loader code without Revit restart (by design)
- Cannot change Revit API registration (dockable panes) dynamically
- Some static state may persist between reloads
- Memory growth over many reloads is expected

## Advanced Scenarios

### Custom MSBuild Integration
The ILRepack and pipe notification is handled by MSBuild target in Rca.Runtime.csproj. 
To customize:
1. Modify the `HotReloadDeploy` target
2. Adjust merge assembly list
3. Change staging directory location
4. Customize pipe command payload

### Multiple Revit Instances  
Each Revit instance runs its own pipe server with same name. The build script connects to the first available one. For multiple instances, consider adding process ID to pipe name.

### CI/CD Integration
The hot reload system is Windows-specific. In CI/CD:
- Linux builds: Code compiles but hot reload features are no-op
- Windows builds: Full functionality available
- Package builds: Use Release configuration to skip hot reload MSBuild target

### Version Tracking
Runtime version includes:
- Assembly version from project
- Optional commit hash from AssemblyMetadata
- Build timestamp in manifest

Example version: `1.2.3.4-abc123def`