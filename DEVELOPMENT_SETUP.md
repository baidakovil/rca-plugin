# RCA Plugin Development Environment Setup

This document provides comprehensive instructions for setting up the development environment for the RCA Plugin project. The project now supports building in both Windows (full development) and CI/Linux environments (testing and validation).

## Table of Contents

- [Prerequisites](#prerequisites)
- [Development Environment Setup](#development-environment-setup)
  - [Windows Development Environment](#windows-development-environment)
  - [CI/Testing Environment (Linux/macOS)](#citesting-environment-linuxmacos)
- [Building the Project](#building-the-project)
- [Architecture Overview](#architecture-overview)
- [Dependencies](#dependencies)
- [Troubleshooting](#troubleshooting)

## Prerequisites

### All Platforms

- **.NET 8 SDK**: [Download from Microsoft](https://dotnet.microsoft.com/download/dotnet/8.0)
  ```bash
  dotnet --version  # Should show 8.0.x or later
  ```

### Windows Development (Full Plugin Development)

- **Visual Studio 2022** or **Visual Studio Code** with C# extension
- **Autodesk Revit 2026** (for full plugin testing)
- **Windows 10/11** (required for WPF support)

### CI/Testing Environment

- **Linux/macOS/Windows** (any platform supporting .NET 8)
- **GitHub Actions** or similar CI platform

## Development Environment Setup

### Windows Development Environment

This setup allows full plugin development including UI components and Revit integration.

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

### CI/Testing Environment (Linux/macOS)

This setup allows building and testing the project structure without Revit dependencies.

#### 1. Setup CI Environment

```bash
# Install .NET 8 SDK (Ubuntu/Debian)
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0

# Or on macOS with Homebrew
brew install dotnet

# Verify installation
dotnet --version
```

#### 2. Clone and Build

```bash
# Clone the repository
git clone https://github.com/baidakovil/rca-plugin.git
cd rca-plugin

# Build the project (uses mock implementations)
dotnet restore
dotnet build

# Run tests (when available)
dotnet test
```

#### 3. Mock Dependencies

In CI/Linux environments, the project automatically uses mock implementations:

- **Rca.Mocks project** - Provides stub implementations for:
  - Autodesk Revit API types (`UIApplication`, `Document`, etc.)
  - WPF types (`Window`, `UserControl`, `CommandManager`)
  - Revit plugin interfaces (`IExternalApplication`, `IExternalCommand`)

#### 4. Conditional Compilation

The project uses conditional compilation to handle platform differences:

```csharp
#if WINDOWS
    // Windows-specific code (WPF, full Revit API)
    var window = new RcaStandaloneWindow();
    window.Show();
#endif
```

## Building the Project

### Command Line Build

```bash
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
├── Rca.Mocks (testing/CI support) ✅
└── Rca.Contracts (no dependencies) ✅
```

### Platform-Specific Behavior

| Component | Windows | Linux/CI |
|-----------|---------|----------|
| **Target Framework** | `net8.0-windows` | `net8.0` |
| **WPF Support** | ✅ Full | ❌ Mocked |
| **Revit API** | ✅ Real DLLs | ❌ Mocked |
| **IronPython** | ✅ Full | ✅ Full |
| **UI Components** | ✅ XAML + WPF | ❌ Excluded |
| **Build/Test** | ✅ Full | ✅ Structure only |

## Dependencies

### Production Dependencies (NuGet)

```xml
<PackageReference Include="IronPython" Version="3.4.2" />
<PackageReference Include="IronPython.StdLib" Version="3.4.2" />
<PackageReference Include="DynamicLanguageRuntime" Version="1.3.5" />
```

### Development Dependencies (Windows Only)

- **RevitAPI.dll** - Autodesk Revit 2026 API
- **RevitAPIUI.dll** - Autodesk Revit 2026 UI API

### Mock Dependencies (CI/Testing)

The `Rca.Mocks` project provides test doubles for:
- All Revit API types and interfaces
- WPF UI components for non-Windows builds
- Plugin lifecycle interfaces

## Troubleshooting

### Common Issues

#### 1. Build Fails: "RevitAPI.dll not found"

**Solution**: Either install Revit 2026 or build in a CI environment where mocks are used automatically.

```bash
# Check if building on correct platform
echo $OS  # Should show 'Windows_NT' for full development

# Force CI build mode (uses mocks)
dotnet build -p:OS=Linux
```

#### 2. Build Fails: "Windows Desktop SDK not found"

**Solution**: The project automatically handles this. Ensure you're using .NET 8 SDK.

```bash
# Check .NET version
dotnet --version

# Clear NuGet cache if needed
dotnet nuget locals all --clear
dotnet restore
```

#### 3. IronPython Dependencies Missing

**Solution**: The NuGet packages should restore automatically.

```bash
# Manual restore
dotnet restore src/Rca.Core/
dotnet restore src/Rca.UI/

# Check package sources
dotnet nuget list source
```

#### 4. Conditional Compilation Issues

**Problem**: Code not compiling due to missing `#if WINDOWS` blocks.

**Solution**: The project automatically sets `WINDOWS` symbol on Windows builds.

```xml
<!-- Automatic in .csproj -->
<DefineConstants Condition="'$(OS)' == 'Windows_NT'">WINDOWS</DefineConstants>
```

### Testing Build in Different Environments

```bash
# Test Windows build (if on Windows)
dotnet build -p:OS=Windows_NT

# Test CI build (any platform)
dotnet build -p:OS=Linux

# Verify mocks are used
dotnet build -v detailed | grep "Rca.Mocks"
```

### GitHub Copilot Development

This project is optimized for GitHub Copilot development:

1. **Consistent naming**: PascalCase for classes/methods, camelCase for fields
2. **XML documentation**: All public APIs documented
3. **SOLID principles**: Clean dependency injection
4. **Testable design**: Interfaces for all services
5. **CI-friendly**: Builds in any environment

```bash
# Verify Copilot-friendly structure
dotnet build  # Should succeed on any platform
dotnet format --verify-no-changes  # Code style validation
```

## Getting Help

- **Issues**: [GitHub Issues](https://github.com/baidakovil/rca-plugin/issues)
- **Discussions**: [GitHub Discussions](https://github.com/baidakovil/rca-plugin/discussions)
- **Revit API**: [Autodesk Developer Network](https://www.autodesk.com/developer-network/platform-technologies/revit)

## Contributing

1. Fork the repository
2. Create a feature branch
3. Ensure builds pass on both Windows and CI
4. Follow the established coding conventions
5. Submit a pull request

The project uses the conventions defined in `.github/copilot-instructions.md`.