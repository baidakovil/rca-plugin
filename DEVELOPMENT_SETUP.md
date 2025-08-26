# RCA Plugin Development Environment Setup

This document provides instructions for setting up the Windows development environment for the RCA Plugin project. Since Revit is a Windows-only application, this plugin is designed exclusively for Windows.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Development Environment Setup](#development-environment-setup)
- [Building the Project](#building-the-project)
- [Dependencies](#dependencies)
- [Troubleshooting](#troubleshooting)

## Prerequisites

### Windows Development Requirements

- **.NET 8 SDK**: [Download from Microsoft](https://dotnet.microsoft.com/download/dotnet/8.0)
  ```powershell
  dotnet --version  # Should show 8.0.x or later
  ```

- **Visual Studio 2022** or **Visual Studio Code** with C# extension
- **Autodesk Revit 2026** (required for plugin development and testing)
- **Windows 10/11** (required for WPF support)

## Development Environment Setup

### Windows Development Environment

This setup provides full plugin development including UI components and Revit integration.

#### 1. Install Required Software

1. **Install .NET 8 SDK**:
   ```powershell
   # Download and install from https://dotnet.microsoft.com/download/dotnet/8.0
   # Or use winget:
   winget install Microsoft.DotNet.SDK.8
   ```

2. **Install Visual Studio 2022** (recommended):
   - Download from [Visual Studio](https://visualstudio.microsoft.com/)
   - Select workloads:
     - .NET desktop development
     - .NET Multi-platform App UI development (for WPF support)

3. **Install Autodesk Revit 2026**:
   - Download from [Autodesk](https://www.autodesk.com/products/revit/)
   - Install to default location: `C:\Program Files\Autodesk\Revit 2026\`

#### 2. Clone and Build

```powershell
# Clone the repository
git clone https://github.com/baidakovil/rca-plugin.git
cd rca-plugin

# Restore packages and build
dotnet restore
dotnet build

# Verify build success
echo $LASTEXITCODE  # Should be 0
```

#### 3. Dependencies Resolved Automatically

On Windows with Revit installed, the following dependencies are automatically resolved:

- **RevitAPI.dll** - Located at `$(ProgramFiles)\Autodesk\Revit 2026\RevitAPI.dll`
- **RevitAPIUI.dll** - Located at `$(ProgramFiles)\Autodesk\Revit 2026\RevitAPIUI.dll`
- **IronPython** - NuGet package `IronPython 3.4.2`
- **IronPython.StdLib** - NuGet package `IronPython.StdLib 3.4.2`
- **DynamicLanguageRuntime** - NuGet package `DynamicLanguageRuntime 1.3.5`

#### 4. Plugin Deployment

The build process automatically:
- Copies the plugin DLL to Revit's add-in directory
- Deploys the `.addin` manifest file
- Copies required dependencies

Default deployment location: `%APPDATA%\Autodesk\Revit\Addins\2026\RcaPlugin\`

## Building the Project

### Command Line Build

```powershell
# Clean build
dotnet clean
dotnet restore
dotnet build

# Release build
dotnet build -c Release

# Build specific project
dotnet build src/Rca.Core/

# Build with verbose output
dotnet build -v detailed
```

### Visual Studio

1. Open `rca-plugin.sln`
2. Right-click solution → "Restore NuGet Packages"
3. Build → "Build Solution" (Ctrl+Shift+B)

## Architecture Overview

The project follows SOLID principles with clean dependency injection:

```
RcaPlugin (composition root)
├── Rca.UI (depends only on Rca.Contracts) ✅
├── Rca.Core (depends only on Rca.Contracts) ✅  
├── Rca.Network (depends only on Rca.Contracts) ✅
└── Rca.Contracts (no dependencies) ✅
```

### Windows-Only Design

| Component | Windows |
|-----------|---------|
| **Target Framework** | `net8.0-windows` |
| **WPF Support** | ✅ Full |
| **Revit API** | ✅ Real DLLs |
| **IronPython** | ✅ Full |
| **UI Components** | ✅ XAML + WPF |

## Dependencies

### Production Dependencies (NuGet)

```xml
<PackageReference Include="IronPython" Version="3.4.2" />
<PackageReference Include="IronPython.StdLib" Version="3.4.2" />
<PackageReference Include="DynamicLanguageRuntime" Version="1.3.5" />
```

### Development Dependencies (Windows Required)

- **RevitAPI.dll** - Autodesk Revit 2026 API
- **RevitAPIUI.dll** - Autodesk Revit 2026 UI API

## Troubleshooting

### Common Issues

#### 1. Build Fails: "RevitAPI.dll not found"

**Solution**: Install Revit 2026 to the default location.

```powershell
# Check Revit installation
Test-Path "C:\Program Files\Autodesk\Revit 2026\RevitAPI.dll"
```

#### 2. Build Fails: "Windows Desktop SDK not found"

**Solution**: Ensure you're using .NET 8 SDK and have Windows Desktop workload installed.

```powershell
# Check .NET version
dotnet --version

# Clear NuGet cache if needed
dotnet nuget locals all --clear
dotnet restore
```

#### 3. IronPython Dependencies Missing

**Solution**: The NuGet packages should restore automatically.

```powershell
# Manual restore
dotnet restore src/Rca.Core/
dotnet restore src/Rca.UI/

# Check package sources
dotnet nuget list source
```

### GitHub Copilot Development

This project is optimized for GitHub Copilot development:

1. **Consistent naming**: PascalCase for classes/methods, camelCase for fields
2. **XML documentation**: All public APIs documented
3. **SOLID principles**: Clean dependency injection
4. **Testable design**: Interfaces for all services
5. **Windows-focused**: Simplified single-platform design

```powershell
# Verify structure
dotnet build  # Should succeed on Windows with Revit
dotnet format --verify-no-changes  # Code style validation
```

## Getting Help

- **Issues**: [GitHub Issues](https://github.com/baidakovil/rca-plugin/issues)
- **Discussions**: [GitHub Discussions](https://github.com/baidakovil/rca-plugin/discussions)
- **Revit API**: [Autodesk Developer Network](https://www.autodesk.com/developer-network/platform-technologies/revit)

## Contributing

1. Fork the repository
2. Create a feature branch
3. Ensure builds pass on Windows
4. Follow the established coding conventions
5. Submit a pull request

The project uses the conventions defined in `.github/copilot-instructions.md`.