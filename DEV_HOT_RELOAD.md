# RCA Plugin Hot Reload Development Guide

This document describes the hot reloading system for the RCA Plugin, enabling zero-restart iterative development in Revit 2026.

## Architecture Overview

The hot reloading system separates the plugin into stable and dynamic components:

```
┌─────────────────────────────────────────────────────────────┐
│                        Revit 2026                           │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │              Stable Components                          │ │
│  │  ┌─────────────────┐    ┌─────────────────┐            │ │
│  │  │   Rca.Loader    │───▶│ Rca.Loader.     │            │ │
│  │  │   (LoaderApp)   │    │ Contracts       │            │ │
│  │  │                 │    │ (IPluginRuntime)│            │ │
│  │  └─────────────────┘    └─────────────────┘            │ │
│  │           │                                             │ │
│  │           ▼                                             │ │
│  │  ┌─────────────────────────────────────────────────────┐ │ │
│  │  │           AssemblyLoadContext (Collectible)         │ │ │
│  │  │  ┌─────────────────────────────────────────────────┐│ │ │
│  │  │  │           Dynamic Components                    ││ │ │
│  │  │  │  ┌─────────────────────────────────────────────┐││ │ │
│  │  │  │  │          Rca.Runtime                        │││ │ │
│  │  │  │  │  (ILRepacked: Rca.Core + Rca.UI +          │││ │ │
│  │  │  │  │              Rca.Network + Rca.Contracts)  │││ │ │
│  │  │  │  └─────────────────────────────────────────────┘││ │ │
│  │  │  └─────────────────────────────────────────────────┘│ │ │
│  │  └─────────────────────────────────────────────────────┘ │ │
│  └─────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                    Build System                            │
│  ┌─────────────────┐    ┌─────────────────┐                │
│  │   MSBuild       │───▶│   Named Pipe    │                │
│  │   ILRepack      │    │   Client        │                │
│  │   Target        │    │   (JSON/RELOAD) │                │
│  └─────────────────┘    └─────────────────┘                │
└─────────────────────────────────────────────────────────────┘
```

## Key Components

### Stable Components (Never Reloaded)
- **Rca.Loader**: Main entry point registered in Revit .addin file
- **Rca.Loader.Contracts**: Interfaces and DTOs for communication

### Dynamic Components (Hot Reloaded)
- **Rca.Runtime**: ILRepacked assembly containing all business logic
- Loaded into collectible AssemblyLoadContext for true unloading on .NET 8

### Communication
- **Named Pipe**: `rca.hotreload` for build system ↔ loader communication
- **JSON Protocol**: Commands (RELOAD) and Events (RELOAD_START, RELOAD_DONE, etc.)

## Development Workflow

### 1. Initial Setup

1. **Install Revit 2026** with .NET 8 target framework
2. **Clone and build** the RCA Plugin solution:
   ```powershell
   git clone https://github.com/baidakovil/rca-plugin.git
   cd rca-plugin
   dotnet build
   ```

3. **Start Revit**: The Loader will automatically deploy and register

### 2. Hot Reload Development

#### Single Build Command (Recommended)
```powershell
# From solution root
dotnet build src/Rca.Runtime -c Debug
```

**What happens automatically:**
1. MSBuild compiles Rca.Runtime and dependencies
2. ILRepack merges assemblies into single `Rca.Dynamic.dll`
3. Files copied to timestamped folder: `%LOCALAPPDATA%\RCA\LiveCore\build_yyyyMMdd_HHmmss\`
4. `current.json` manifest updated with new folder path
5. RELOAD command sent via named pipe to running Loader
6. Loader unloads previous AssemblyLoadContext and loads new runtime
7. Panel reflects changes **without restarting Revit**

#### Manual Reload (Fallback)
```powershell
# PowerShell script for manual triggering
.\tools\SendReload.ps1

# Or specify specific folder
.\tools\SendReload.ps1 -Folder "C:\Users\%USERNAME%\AppData\Local\RCA\LiveCore\build_20241201_143022"
```

#### Manual Reload Button
- In Revit: **RCA Loader** tab → **Hot Reload** panel → **Reload Runtime** button

### 3. Development Tips

#### Making Changes
1. **Edit source code** in `src/Rca.Core`, `src/Rca.UI`, `src/Rca.Network`, or `src/Rca.Runtime`
2. **Build Rca.Runtime**: `dotnet build src/Rca.Runtime -c Debug`
3. **Observe changes** in Revit panel immediately

#### Verifying Reload
- Console output shows reload events (in Debug builds)
- Check `%LOCALAPPDATA%\RCA\LiveCore\current.json` for latest folder
- Runtime version displays in Revit (includes commit hash in Debug)

#### Testing Scenarios
```powershell
# Example: Change button text in RcaRuntime.cs
# 1. Edit ButtonText constant
# 2. Build runtime
dotnet build src/Rca.Runtime -c Debug
# 3. Button text updates in Revit immediately
```

## File Structure

### Runtime Staging
```
%LOCALAPPDATA%\RCA\LiveCore\
├── current.json              # Manifest pointing to active runtime
├── build_20241201_143022\    # Timestamped runtime folder
│   └── Rca.Dynamic.dll       # ILRepacked runtime assembly
├── build_20241201_143055\    # Previous builds (kept for debugging)
│   └── Rca.Dynamic.dll
└── ...
```

### Solution Structure
```
src/
├── Rca.Loader.Contracts/     # 🔗 Hot reload interfaces and DTOs
├── Rca.Loader/               # 🔄 Stable loader with pipe server
├── Rca.Runtime/              # 🚀 Hot-reloadable business logic
├── Rca.Core/                 # 🧠 Business logic (referenced by Runtime)
├── Rca.UI/                   # 🎨 WPF panels (referenced by Runtime)  
├── Rca.Network/              # 🌐 Network services (referenced by Runtime)
├── Rca.Contracts/            # 📋 Domain interfaces (referenced by Runtime)
└── RcaPlugin/                # ⚠️ Legacy (will be deprecated)

tools/
└── SendReload.ps1            # 🔧 Manual reload trigger script
```

## Named Pipe Protocol

### Commands (Build System → Loader)
```json
{
  "command": "RELOAD",
  "payload": {
    "folder": "C:\\Users\\...\\RCA\\LiveCore\\build_20241201_143022",
    "assembly": "Rca.Dynamic.dll"
  }
}
```

### Events (Loader → Build System)
```json
// Success
{ "event": "RELOAD_ACCEPTED" }
{ "event": "RELOAD_START" }  
{ "event": "RELOAD_DONE" }

// Error
{ 
  "event": "RELOAD_FAIL",
  "payload": {
    "message": "Assembly not found",
    "exception": "FileNotFoundException: ..."
  }
}
```

## Troubleshooting

### Memory Growth
- **Expected**: Some memory growth after reloads due to .NET internals
- **Problematic**: Unbounded growth after 10+ reloads
- **Debug**: Check WeakReference logs in DEBUG builds for ALC collection

### Stale Static State
- **Symptom**: Old behavior persists after reload
- **Cause**: Static fields/singletons not resetting
- **Solution**: Use instance-based services via dependency injection

### Assembly Lock Issues
- **Symptom**: ILRepack fails with "file in use" error
- **Cause**: Previous AssemblyLoadContext not fully unloaded
- **Solution**: Wait 5-10 seconds before retry, or restart Revit

### Pipe Communication Issues
- **Symptom**: "No reload pipe available" message
- **Cause**: Loader not running or pipe server crashed
- **Solution**: Use manual reload button or restart Revit

### Build Failures
```powershell
# Clean build if needed
dotnet clean
dotnet restore  
dotnet build src/Rca.Runtime -c Debug
```

## Performance Notes

- **Build Time**: ~5-15 seconds for ILRepack + deploy
- **Reload Time**: ~1-3 seconds for AssemblyLoadContext swap
- **Memory Impact**: ~10-50 MB growth per reload (collectible after GC)
- **Stability**: Tested for 50+ reloads without issues

## AI Agent Integration

For **GitHub Copilot** and other AI development assistants:

### Single Command Workflow
```powershell
# This is the ONLY command needed for hot reload development:
dotnet build src/Rca.Runtime -c Debug
```

### Testing Changes
1. Make code changes in any referenced project
2. Run the build command above  
3. Changes appear in Revit panel immediately
4. No manual steps required

### Error Recovery
```powershell
# If reload fails, try manual reload:
.\tools\SendReload.ps1

# If that fails, use Revit button:
# RCA Loader tab → Hot Reload panel → Reload Runtime button
```

## Advanced Configuration

### Disable Hot Reload (Production)
```xml
<!-- In Rca.Runtime.csproj -->
<Target Name="HotReloadPack" AfterTargets="Build" Condition="'$(Configuration)' == 'Debug' AND '$(EnableHotReload)' != 'false'">
```

### Custom Pipe Name
```csharp
// In HotReloadConstants.cs
public const string PipeName = "custom.reload.pipe";
```

### Extended Timeout
```powershell
.\tools\SendReload.ps1 -TimeoutMs 10000
```

---

## Quick Reference

| Action | Command |
|--------|---------|
| **Hot Reload** | `dotnet build src/Rca.Runtime -c Debug` |
| **Manual Reload** | `.\tools\SendReload.ps1` |
| **Clean Build** | `dotnet clean && dotnet build` |
| **Check Manifest** | `type "%LOCALAPPDATA%\RCA\LiveCore\current.json"` |
| **View Logs** | Check Visual Studio Output window |

**🎯 Remember**: Just build `src/Rca.Runtime` and watch your changes appear in Revit instantly!