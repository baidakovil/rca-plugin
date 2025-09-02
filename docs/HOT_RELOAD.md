# RCA Plugin Hot-Reload Infrastructure

This document describes the hot-reload infrastructure for the RCA Plugin, enabling rapid development without restarting Revit.

## Overview

The hot-reload system consists of two main components:

1. **Rca.Loader** - A minimal loader that runs inside Revit and can dynamically load/unload the main plugin
2. **RcaReloadTrigger** - A command-line utility for triggering reloads via named pipes

## Architecture

```
┌─────────────────────────────────────────────────────┐
│                    Revit 2026                       │
│  ┌───────────────────────────────────────────────┐  │
│  │              Rca.Loader                       │  │
│  │  ┌─────────────────┐  ┌─────────────────────┐ │  │
│  │  │ PluginLoader    │  │   NamedPipeService  │ │  │
│  │  │ Service         │  │                     │ │  │
│  │  │                 │  │   ┌─────────────┐   │ │  │
│  │  │ ┌─────────────┐ │  │   │ "RcaPlugin  │   │ │  │
│  │  │ │AssemblyLoad │ │  │   │ Reloader"   │   │ │  │
│  │  │ │Context      │ │  │   │ Pipe Server │   │ │  │
│  │  │ │ (ALC)       │ │  │   └─────────────┘   │ │  │
│  │  │ └─────────────┘ │  └─────────────────────┘ │  │
│  │  └─────────────────┘                          │  │
│  │           │                        ▲          │  │
│  │           ▼                        │          │  │
│  │  ┌─────────────────┐               │          │  │
│  │  │   RcaPlugin     │               │          │  │
│  │  │   (Loaded)      │               │          │  │
│  │  └─────────────────┘               │          │  │
│  └───────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
                                        │
                                        │ Named Pipe
                                        │ Communication
                                        │
┌─────────────────────────────────────────────────────┐
│              Development Environment                │
│  ┌───────────────────────────────────────────────┐  │
│  │           RcaReloadTrigger                    │  │
│  │                                               │  │
│  │  ┌─────────────────────────────────────────┐  │  │
│  │  │         Commands:                       │  │  │
│  │  │  • reload [--assembly path]             │  │  │
│  │  │  • ping                                 │  │  │
│  │  │  • status                               │  │  │
│  │  └─────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────┘  │
│                                                     │
│  ┌───────────────────────────────────────────────┐  │
│  │         PowerShell Script                     │  │
│  │         hot-reload.ps1                        │  │
│  │                                               │  │
│  │  1. Build solution                            │  │
│  │  2. Trigger reload                            │  │
│  │  3. Automated workflow                        │  │
│  └───────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
```

## Key Features

### 1. Assembly Load Context Isolation

The `PluginLoaderService` uses .NET's `AssemblyLoadContext` to:
- Load plugin assemblies in an isolated context
- Enable safe unloading of assemblies
- Prevent assembly version conflicts
- Support garbage collection of unloaded assemblies

### 2. Named Pipe Communication

The `NamedPipeService` provides:
- Inter-process communication between development tools and Revit
- Command protocol for reload operations
- Real-time status monitoring
- Reliable connection handling

### 3. Hot-Reload Commands

Available commands through the named pipe:
- `RELOAD` - Reload the main plugin
- `RELOAD|path` - Reload a specific assembly
- `PING` - Test connection
- `STATUS` - Get loader status

## Setup Instructions

### 1. Install the Loader

1. Build the solution:
   ```powershell
   dotnet build -c Release
   ```

2. The loader will be automatically deployed to:
   ```
   %APPDATA%\Autodesk\Revit\Addins\2026\RcaLoader.addin
   %APPDATA%\Autodesk\Revit\Addins\2026\RcaLoader\
   ```

3. Start Revit 2026 - the loader will automatically start

### 2. Development Workflow

#### Option A: PowerShell Script (Recommended)

```powershell
# Build and reload
.\scripts\hot-reload.ps1

# Build only
.\scripts\hot-reload.ps1 -BuildOnly

# Reload only (if already built)
.\scripts\hot-reload.ps1 -ReloadOnly

# Verbose output
.\scripts\hot-reload.ps1 -Verbose
```

#### Option B: Manual Commands

```powershell
# Build the plugin
dotnet build

# Trigger reload
.\bin\Debug\net8.0-windows\RcaReloadTrigger.exe reload

# Test connection
.\bin\Debug\net8.0-windows\RcaReloadTrigger.exe ping

# Check status
.\bin\Debug\net8.0-windows\RcaReloadTrigger.exe status
```

### 3. VS Code Integration

Add to `.vscode/tasks.json`:

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "Hot Reload RCA Plugin",
      "type": "shell",
      "command": "powershell",
      "args": [
        "-ExecutionPolicy", "Bypass",
        "-File", "${workspaceFolder}/scripts/hot-reload.ps1"
      ],
      "group": {
        "kind": "build",
        "isDefault": true
      },
      "presentation": {
        "echo": true,
        "reveal": "always",
        "focus": false,
        "panel": "shared"
      },
      "problemMatcher": "$msCompile"
    }
  ]
}
```

Then use `Ctrl+Shift+P` → "Tasks: Run Task" → "Hot Reload RCA Plugin"

## Configuration

### Named Pipe Settings

The default pipe name is `RcaPluginReloader`. To customize:

```csharp
// In RcaLoaderApp.cs
private const string PipeName = "MyCustomPipeName";
```

```powershell
# In hot-reload.ps1
.\scripts\hot-reload.ps1 -PipeName "MyCustomPipeName"
```

### Assembly Paths

The loader automatically looks for the main plugin at:
```
RcaLoader\RcaPlugin\RcaPlugin.dll
```

To specify a custom path:
```powershell
RcaReloadTrigger.exe reload --assembly "C:\path\to\custom\plugin.dll"
```

## Troubleshooting

### Common Issues

1. **Connection timeout**
   - Ensure Revit is running with the RCA Loader
   - Check Windows Firewall settings
   - Verify pipe name matches

2. **Assembly loading fails**
   - Check file permissions
   - Ensure all dependencies are present
   - Verify assembly contains IExternalApplication implementation

3. **Build errors**
   - Run `dotnet clean` then `dotnet build`
   - Check for file locks (close Revit if necessary)
   - Verify .NET 8 SDK is installed

### Debug Output

Enable verbose logging:
```powershell
.\scripts\hot-reload.ps1 -Verbose
```

### Testing Connection

Test the pipe connection:
```powershell
RcaReloadTrigger.exe ping
```

Expected output:
```
Connecting to pipe 'RcaPluginReloader'...
Connected!
Sending: PING
Response: OK|Pong
```

## Development Tips

1. **Rapid Iteration**: Use the PowerShell script with a keyboard shortcut for instant builds and reloads

2. **Assembly Dependencies**: Keep the main plugin dependencies minimal to reduce reload time

3. **State Management**: Design plugin services to be stateless where possible to handle reloads gracefully

4. **Error Handling**: Always handle exceptions in the main plugin as crashes could affect the loader

5. **Resource Cleanup**: Implement proper disposal patterns for resources that need cleanup between reloads

## Performance Considerations

- Initial load time: ~100-500ms
- Reload time: ~50-200ms
- Memory overhead: Minimal (isolated contexts are garbage collected)
- Pipe communication: <10ms latency

## Security Notes

- Named pipes are local to the machine
- No network communication involved
- Standard Windows access controls apply
- Only local processes can connect to the pipe