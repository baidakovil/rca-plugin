# RCA Plugin - Revit Chat Assistant

A modern Revit plugin scaffold implementing SOLID principles with dependency injection, featuring a Python execution engine, dockable UI panel, and **hot reload development experience**.

## Quick Start

### Windows Development (Revit Plugin)
```powershell
# Requires: .NET 8 SDK + Revit 2026
git clone https://github.com/baidakovil/rca-plugin.git
cd rca-plugin
dotnet build
```

### 🔥 Hot Reload Development
```powershell
# Make code changes, then rebuild runtime for instant updates:
dotnet build src/Rca.Runtime

# No Revit restart needed! Changes appear immediately.
# See DEV_HOT_RELOAD.md for details.
```

## Features

- ✅ **SOLID Architecture**: Clean dependency injection with interface segregation
- ✅ **Python Engine**: IronPython 3.4.2 integration for dynamic scripting
- ✅ **Windows Desktop**: Native WPF dockable panel with MVVM pattern
- ✅ **Testable Design**: All services injectable and mockable
- ✅ **GitHub Copilot Ready**: Consistent conventions and documentation
- 🔥 **Hot Reload**: Update code without restarting Revit (saves hours during development)

## Dependencies Handled Automatically

### Windows Development
- **Revit API**: `RevitAPI.dll`, `RevitAPIUI.dll` (from Revit 2026 installation)
- **Python**: `IronPython 3.4.2`, `DynamicLanguageRuntime 1.3.5` (NuGet)
- **UI**: WPF (.NET 8 Windows Desktop)

## Development Experience

### Traditional Workflow
```
Edit Code → Build → Close Revit → Start Revit → Load Plugin → Test
⏱️ 5-10 minutes per iteration
```

### With Hot Reload
```
Edit Code → Build Runtime → Test Immediately
⏱️ 10-30 seconds per iteration
```

**10-20x faster development cycle!**

## Architecture

```
RcaPlugin (composition root)
├── Rca.UI ──────────┐
├── Rca.Core ────────┼──► Rca.Contracts (interfaces only)
└── Rca.Network ─────┘
```

**Before** (❌ Violated DIP):
```
Rca.UI ──► Rca.Core (direct dependency - tight coupling)
```

**After** (✅ Follows DIP):
```
Rca.UI ──► Rca.Contracts ◄── Rca.Core (loose coupling via interfaces)
```

## Project Structure

```
src/
├── Rca.Loader.Contracts/ # 🔗 Hot reload interfaces and protocol
├── Rca.Loader/           # 🏗️  Stable loader with hot reload management
├── Rca.Runtime/          # ⚡ Hot-swappable runtime (main plugin logic)
├── Rca.Contracts/        # 📋 Core interfaces and contracts  
├── Rca.Core/             # 🧠 Business logic and Python engine
├── Rca.UI/               # 🎨 WPF dockable panel and views
├── Rca.Network/          # 🌐 Network services
└── RcaPlugin/            # 🚀 Legacy plugin entry point (deprecated)
```

## Development Setup

📖 **[Complete Development Setup Guide](DEVELOPMENT_SETUP.md)**

🔥 **[Hot Reload Development Guide](DEV_HOT_RELOAD.md)** ← **Start here for fast development**

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