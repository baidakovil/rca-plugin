# RCA Plugin Hot Reload Development Guide

This document explains the hot reload functionality that enables developers to update the RCA Plugin code without restarting Revit.

## Architecture Overview

The hot reload system consists of three main components:

```
┌─────────────────┐    Named Pipe     ┌──────────────────┐
│   Rca.Loader    │ ◄────────────────► │   Build System   │
│   (Stable)      │   JSON Commands   │   (MSBuild)      │
└─────────────────┘                   └──────────────────┘
         │                                       │
         │ Manages                               │ Creates
         ▼                                       ▼
┌─────────────────┐                   ┌──────────────────┐
│ Rca.Dynamic.dll │                   │  current.json    │
│   (Hot-swapped) │                   │   (Manifest)     │
└─────────────────┘                   └──────────────────┘
```

### Components

1. **Rca.Loader** - Stable loader that never unloads
   - Manages AssemblyLoadContext lifecycle
   - Hosts Named Pipe server for build notifications
   - Provides manual reload command via ribbon

2. **Rca.Runtime** - Hot-swappable runtime implementation
   - Contains main plugin logic (moved from RcaPluginApp)
   - Implements IPluginRuntime interface
   - Merged with dependencies via ILRepack into Rca.Dynamic.dll

3. **Rca.Loader.Contracts** - Stable contracts
   - IPluginRuntime interface
   - Named pipe protocol DTOs
   - Constants and configuration

## Quick Start

### 1. Initial Setup

The plugin is automatically configured for hot reload. Deploy the loader:

1. Build the solution: `dotnet build`
2. Start Revit - the RCA Loader will load automatically
3. You'll see two ribbon tabs:
   - **RCA Loader** - Manual reload controls
   - **RCA Plugin** - Main plugin functionality (from runtime)

### 2. Development Workflow

To update plugin code without restarting Revit:

```powershell
# Make changes to any code in Rca.Core, Rca.UI, Rca.Network, or Rca.Runtime
# Then build just the runtime project:
dotnet build src/Rca.Runtime -c Debug

# The plugin will automatically reload in Revit!
```

**That's it!** No Revit restart required.

### 3. Manual Reload (Fallback)

If automatic reload fails, use the manual option:

1. Click **RCA Loader** → **Hot Reload** → **Reload Runtime** in Revit
2. Or use PowerShell: `.\tools\SendReload.ps1`

## How It Works

### Build Process

When you build `Rca.Runtime`:

1. **ILRepack** merges all dependencies into `Rca.Dynamic.dll`
2. **Staging** copies the merged assembly to timestamped folder: `%APPDATA%\RCA\LiveCore\build_yyyyMMdd_HHmmss\`
3. **Manifest** updates `current.json` with new folder location
4. **Notification** sends `RELOAD` command via Named Pipe to loader

### Runtime Loading

The Loader continuously:

1. **Monitors** Named Pipe for `RELOAD` commands
2. **Unloads** current AssemblyLoadContext (if any)
3. **Loads** new `Rca.Dynamic.dll` into fresh collectible context
4. **Initializes** IPluginRuntime implementation
5. **Registers** ribbons and dockable panels

### Memory Management

- Uses .NET 8 **collectible AssemblyLoadContext** for true unloading
- **Garbage collection** is forced after each unload
- **WeakReference** tracking verifies context collection (DEBUG mode)
- **Memory growth** should be minimal over multiple reloads

## File Structure

```
%APPDATA%\RCA\LiveCore\
├── current.json                    # Points to current runtime
├── build_20241201_143022\         # Timestamped builds
│   └── Rca.Dynamic.dll
├── build_20241201_143105\
│   └── Rca.Dynamic.dll
└── ...                            # Older builds (kept for rollback)
```

### Manifest Format (current.json)

```json
{
  "folder": "C:\\Users\\...\\AppData\\Local\\RCA\\LiveCore\\build_20241201_143022",
  "assembly": "Rca.Dynamic.dll",
  "timestamp": "20241201_143022",
  "version": "1.0.0.20241201_143022"
}
```

## Named Pipe Protocol

Communication uses JSON messages over Named Pipe `rca.hotreload`:

### Commands (Client → Loader)

```json
{
  "command": "RELOAD",
  "payload": {
    "folder": "optional/override/path"
  }
}
```

### Events (Loader → Client)

```json
{
  "event": "RELOAD_ACCEPTED|RELOAD_START|RELOAD_DONE|RELOAD_FAIL|RUNTIME_ERROR",
  "timestamp": "2024-12-01T14:30:22.123Z",
  "data": { ... }
}
```

## Development Tips

### Making Changes

1. **Code Changes**: Edit any files in Rca.Core, Rca.UI, Rca.Network, or Rca.Runtime
2. **Build Runtime**: `dotnet build src/Rca.Runtime`
3. **Test Changes**: Use functionality in Revit - changes are live!

### Testing Reload

Change a visible string constant and rebuild:

```csharp
// In Rca.Runtime/RcaRuntime.cs
private const string ButtonText = "Chat Assistant v2"; // Change this
```

Build and observe the button text update in Revit without restart.

### Debugging

- **Console Output**: Check Visual Studio Output window for loader messages
- **Debug Builds**: Automatic reload notifications shown via TaskDialog
- **Manual Reload**: Use PowerShell script for detailed pipe communication
- **Memory Tracking**: WeakReference collection status in debug output

### AI Agent Usage (GitHub Copilot)

This project is optimized for AI development:

```powershell
# Single command to test changes:
dotnet build src/Rca.Runtime

# Manual reload for testing:
.\tools\SendReload.ps1

# Full rebuild (if needed):
dotnet build
```

The AI agent should:
1. Make changes to business logic in Core/UI/Network projects
2. Update runtime in Rca.Runtime if needed
3. Build only Rca.Runtime to trigger hot reload
4. Verify changes work in running Revit instance

## Troubleshooting

### Reload Not Working

1. **Check Revit**: Is RCA Loader tab visible? If not, deploy failed
2. **Check Manifest**: Does `%APPDATA%\RCA\LiveCore\current.json` exist?
3. **Check Build**: Did `dotnet build src/Rca.Runtime` succeed?
4. **Check Pipe**: Use `.\tools\SendReload.ps1` for manual test

### Memory Issues

1. **Monitor Growth**: Task Manager → Revit process memory
2. **Force Collection**: Each reload triggers GC.Collect()
3. **Static References**: Avoid static event handlers in runtime code
4. **Cleanup**: Runtime.Shutdown() should dispose resources

### Build Errors

1. **ILRepack Failed**: Check dependencies are copy-local
2. **Missing References**: Ensure all projects reference correctly
3. **Permission Issues**: Check write access to %APPDATA%\RCA

### Common Issues

| Problem | Solution |
|---------|----------|
| "No runtime manifest found" | Build Rca.Runtime project first |
| "Runtime assembly not found" | Check %APPDATA%\RCA\LiveCore folder exists |
| "Could not notify loader" | Revit not running or loader failed to start |
| Memory keeps growing | Check for static references or event leaks |
| Changes not reflected | Verify correct project was built |

## Advanced Usage

### Custom Build Scripts

For automated development workflows:

```powershell
# Watch and rebuild
dotnet watch build src/Rca.Runtime

# Build with custom versioning
dotnet build src/Rca.Runtime -p:AssemblyVersion=1.2.3.4
```

### Multiple Developers

Each developer can work independently:
- Staged builds use timestamps to avoid conflicts
- Named pipe is per-user (local machine scope)
- No shared state between developer instances

### CI/CD Integration

The hot reload system doesn't interfere with normal builds:
- CI builds produce standard assemblies
- Hot reload targets only run on Windows with Revit
- Linux CI continues to work normally

## Limitations

1. **Windows Only**: Named Pipes and WPF require Windows
2. **Single Instance**: One loader per user session
3. **Forward Compatibility**: New interfaces require loader restart
4. **Complex Statics**: Global state may not reload cleanly

## Performance

- **Reload Time**: ~2-5 seconds including build
- **Memory Overhead**: ~10-50MB per reload (temporary)
- **Build Time**: Only runtime project (~10-30s)
- **Startup Impact**: Minimal, loader starts quickly

---

For questions or issues, check the GitHub repository issues or discussions.