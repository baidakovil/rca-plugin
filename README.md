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
- ✅ **Hot Reload**: Zero-restart development with collectible AssemblyLoadContext
- ✅ **Testable Design**: All services injectable and mockable
- ✅ **GitHub Copilot Ready**: Consistent conventions and documentation

## Dependencies Handled Automatically

### Windows Development
- **Revit API**: `RevitAPI.dll`, `RevitAPIUI.dll` (from Revit 2026 installation)
- **Python**: `IronPython 3.4.2`, `DynamicLanguageRuntime 1.3.5` (NuGet)
- **UI**: WPF (.NET 8 Windows Desktop)

## Hot Reload Development

⚡ **Zero-restart iterative development in Revit 2026**

```powershell
# Single command triggers hot reload
dotnet build src/Rca.Runtime -c Debug
```

**What happens automatically:**
1. Code compiles with ILRepack into single dynamic assembly  
2. Named pipe notifies running Revit loader
3. AssemblyLoadContext unloads old code and loads new
4. Changes appear in panel **instantly without restarting Revit**

📖 **[Complete Hot Reload Guide](DEV_HOT_RELOAD.md)**

## Architecture

```
Hot Reload Architecture:
┌─────────────────────────────────────────────────────┐
│                    Revit 2026                       │
│  ┌─────────────────────────────────────────────────┐ │
│  │ Stable Loader (Never Reloaded)                 │ │
│  │ ├── Rca.Loader (LoaderApp)                     │ │  
│  │ └── Rca.Loader.Contracts                       │ │
│  │           │                                     │ │
│  │           ▼                                     │ │
│  │ ┌─────────────────────────────────────────────┐ │ │
│  │ │ Dynamic Runtime (Hot Reloaded)              │ │ │
│  │ │ └── Rca.Runtime (ILRepacked)                │ │ │
│  │ │     ├── Rca.Core                            │ │ │
│  │ │     ├── Rca.UI                              │ │ │
│  │ │     ├── Rca.Network                         │ │ │
│  │ │     └── Rca.Contracts                       │ │ │
│  │ └─────────────────────────────────────────────┘ │ │
│  └─────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────┘
```

**Traditional Architecture (Legacy)**:
```
RcaPlugin (composition root) [DEPRECATED]
├── Rca.UI ──────────┐
├── Rca.Core ────────┼──► Rca.Contracts (interfaces only)
└── Rca.Network ─────┘
```

## Project Structure

```
src/
├── Rca.Loader.Contracts/   # 🔗 Hot reload interfaces and DTOs
├── Rca.Loader/             # 🔄 Stable loader with pipe server  
├── Rca.Runtime/            # 🚀 Hot-reloadable business logic
├── Rca.Contracts/          # 📋 Domain interfaces and contracts
├── Rca.Core/               # 🧠 Business logic and Python engine  
├── Rca.UI/                 # 🎨 WPF dockable panel and views
├── Rca.Network/            # 🌐 Network services
└── RcaPlugin/              # ⚠️ Legacy entry point (deprecated)

tools/
└── SendReload.ps1          # 🔧 Manual hot reload trigger
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