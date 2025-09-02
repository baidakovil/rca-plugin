# RCA Plugin - Revit Chat Assistant

A modern Revit plugin scaffold implementing SOLID principles with dependency injection, featuring a Python execution engine and dockable UI panel.

## Quick Start

### Windows Development (Revit Plugin)
```powershell
# Requires: .NET 8 SDK + Revit 2026
git clone https://github.com/baidakovil/rca-plugin.git
cd rca-plugin
dotnet build
```

## Features

- ✅ **SOLID Architecture**: Clean dependency injection with interface segregation
- ✅ **Python Engine**: IronPython 3.4.2 integration for dynamic scripting
- ✅ **Windows Desktop**: Native WPF dockable panel with MVVM pattern
- ✅ **Testable Design**: All services injectable and mockable
- ✅ **GitHub Copilot Ready**: Consistent conventions and documentation
- ✅ **Hot-Reload**: AssemblyLoadContext-based plugin reloading without restarting Revit
- ✅ **Development Tools**: PowerShell scripts and VS Code integration for rapid iteration

## Dependencies Handled Automatically

### Windows Development
- **Revit API**: `RevitAPI.dll`, `RevitAPIUI.dll` (from Revit 2026 installation)
- **Python**: `IronPython 3.4.2`, `DynamicLanguageRuntime 1.3.5` (NuGet)
- **UI**: WPF (.NET 8 Windows Desktop)

## Architecture

```
RcaLoader (hot-reload loader)
├── Rca.Loader ──────────┐
│   ├── PluginLoader     │
│   └── NamedPipeService │
└──► Plugin Loading ─────┘
     │
     ▼
RcaPlugin (main plugin - dynamically loaded)
├── Rca.UI ──────────┐
├── Rca.Core ────────┼──► Rca.Contracts (interfaces only)
└── Rca.Network ─────┘
```

**Hot-Reload Flow**:
```
Developer → hot-reload.ps1 → RcaReloadTrigger → Named Pipe → RcaLoader → Unload/Load Plugin
```

**Dependencies** (✅ Follows DIP):
```
Rca.UI ──► Rca.Contracts ◄── Rca.Core (loose coupling via interfaces)
```

## Project Structure

```
src/
├── Rca.Contracts/       # 🔗 Interfaces and contracts
├── Rca.Core/            # 🧠 Business logic and Python engine  
├── Rca.UI/              # 🎨 WPF dockable panel and views
├── Rca.Network/         # 🌐 Network services
├── Rca.Loader/          # 🔄 Hot-reload loader with AssemblyLoadContext
├── RcaReloadTrigger/    # ⚡ Command-line reload trigger utility
└── RcaPlugin/           # 🚀 Main plugin (dynamically loaded)
```

## Development Setup

📖 **[Complete Development Setup Guide](DEVELOPMENT_SETUP.md)**

### Quick Environment Check

```powershell
# Verify .NET 8 installation
dotnet --version  # Should show 8.0.x

# Check Revit installation
Test-Path "C:\Program Files\Autodesk\Revit 2026\"  # Should return True

# Build the project
dotnet build  # Should succeed on Windows with Revit installed
```

## Hot-Reload Development Workflow

### Quick Start
```powershell
# Build and reload in one command
.\scripts\hot-reload.ps1

# Or use VS Code: Ctrl+Shift+P → "Tasks: Run Task" → "Hot Reload RCA Plugin"
```

### Manual Steps
```powershell
# 1. Start Revit 2026 (loads RcaLoader automatically)
# 2. Build and reload
dotnet build
.\bin\Debug\net8.0-windows\RcaReloadTrigger.exe reload

# 3. Test changes instantly without restarting Revit!
```

📖 **[Detailed Hot-Reload Documentation](docs/HOT_RELOAD.md)**

## Building and Testing

```powershell
# Clean build
dotnet clean && dotnet restore && dotnet build

# Release build  
dotnet build -c Release
```

## Deployment

The plugin automatically deploys to:
```
%APPDATA%\Autodesk\Revit\Addins\2026\RcaPlugin\
```

Files deployed:
- `RcaPlugin.dll` (main plugin)
- `Rca.*.dll` (dependencies)
- `RcaPlugin._noload_addin` (manifest)
- IronPython libraries

## Usage

1. **Start Revit 2026** (Windows)
2. **Access Plugin**: 
   - Ribbon → "Add-Ins" tab → "RCA Panel" 
   - Or: Standalone window via "RCA Standalone"
3. **Execute Python**: Enter code and click "Hello from Python!"
4. **View Logs**: Click "Show Debug Info" for execution details

## Contributing

This project follows GitHub Copilot conventions:

- **Naming**: PascalCase (public), camelCase (private)  
- **Documentation**: XML docs on all public APIs
- **Architecture**: SOLID principles with DI
- **Testing**: Interface-based mocking
- **CI**: Cross-platform builds

See [Development Setup](DEVELOPMENT_SETUP.md) for detailed instructions.

## License

MIT License - see LICENSE file for details.

---

🔧 **Need Help?** Check the [Development Setup Guide](DEVELOPMENT_SETUP.md) for comprehensive environment setup instructions.