# RCA Plugin Development Instructions

**ALWAYS follow these instructions first. Only search for additional information if these instructions are incomplete or found to be in error.**

RCA Plugin (Revit Chat Assistant) is a .NET 8 Windows-only Revit 2026 add-in that provides an AI chat interface embedded in a dockable panel. The project uses SOLID architecture with dependency injection, IronPython 3.4.2 for scripting, and WPF for the UI.

## Critical Requirements

**This project ONLY builds and runs on Windows with Revit 2026 installed. Do not attempt to build on Linux/macOS.**

### Windows Development Environment

Install these components in order:
1. **.NET 8 SDK** - Download from https://dotnet.microsoft.com/download/dotnet/8.0
2. **Visual Studio 2022** with workloads:
   - .NET desktop development  
   - .NET Multi-platform App UI development (for WPF)
3. **Autodesk Revit 2026** - Must be installed to default location: `C:\Program Files\Autodesk\Revit 2026\`
4. **Windows 10/11** - Required for WPF support

Verify installation:
```powershell
dotnet --version  # Should show 8.0.x
Test-Path "C:\Program Files\Autodesk\Revit 2026\"  # Should return True
```

## Building the Project

### NEVER CANCEL - Build Commands with Timeouts

**CRITICAL**: Set timeouts of 15+ minutes for all build commands. Builds may take 10+ minutes on slower machines.

```powershell
# Clean build sequence - NEVER CANCEL - Takes 5-15 minutes
dotnet clean rca-plugin.sln
dotnet restore rca-plugin.sln  # Takes 2-5 minutes
dotnet build rca-plugin.sln   # Takes 5-10 minutes

# Release build - NEVER CANCEL - Takes 10-15 minutes  
dotnet build rca-plugin.sln -c Release
```

### Build Validation
After building, verify these files exist:
- `bin/Debug/net8.0-windows/RcaPlugin.dll`
- `%APPDATA%\Autodesk\Revit\Addins\2026\RcaPlugin\` (auto-deployed)
- IronPython DLLs in output directory

## Testing

### NEVER CANCEL - Test Commands with Timeouts

**CRITICAL**: Set timeouts of 10+ minutes for test commands. Test discovery and execution may take 5-10 minutes.

```powershell
# Run all tests - NEVER CANCEL - Takes 5-10 minutes
dotnet test rca-plugin.sln

# Run specific test categories - NEVER CANCEL - Takes 2-5 minutes each  
dotnet test --filter Category=Unit
dotnet test --filter Category=Revit  # Requires Revit running
```

### Test Categories
- **Unit Tests**: Mock-based tests, run without Revit
- **Revit Tests**: Integration tests requiring Revit 2026 running
- **UI Tests**: WPF component tests

## Manual Validation Scenarios

**ALWAYS perform these validation steps after making changes:**

### Core Functionality Test
1. **Start Revit 2026**
2. **Access Plugin**: Ribbon → "Add-Ins" tab → "RCA Panel"
3. **Test Dockable Panel**: Enter Python code: `print("Hello World")`
4. **Execute**: Click "Hello from Python!" button
5. **Verify Output**: Should show "Hello World" in output area
6. **Test Standalone**: Click "RCA Standalone" to test window mode

### Advanced Validation
1. **Test Python Context**: Execute `doc = __revit__.ActiveUIDocument.Document` 
2. **Test Error Handling**: Enter invalid Python and verify error display
3. **Test Debug Info**: Click "Show Debug Info" and verify logs appear
4. **Test Panel Docking**: Undock/redock the panel in different positions

## Code Style and Linting

**ALWAYS run these before committing - NEVER CANCEL - Takes 2-5 minutes:**

```powershell
# Format code - NEVER CANCEL
dotnet format rca-plugin.sln

# Verify no formatting changes needed
dotnet format rca-plugin.sln --verify-no-changes
```

## Project Architecture

**5-Project SOLID Architecture:**
```
RcaPlugin/ (composition root)
├── Rca.UI/       → Rca.Contracts (WPF views, MVVM)
├── Rca.Core/     → Rca.Contracts (Python engine, services)  
├── Rca.Network/  → Rca.Contracts (future network features)
└── Rca.Contracts/               (interfaces only)
```

**6 Test Projects:** Each main project has corresponding `.Tests` project.

### Codebase Statistics
- **28 C# source files** across projects
- **2 XAML files** for WPF UI components
- **5 main projects** following SOLID principles
- **6 test projects** with comprehensive coverage

### Key Source Files
- `RcaPluginApp.cs` - Main Revit application entry point
- `ShowDockablePanelCommand.cs` - Command to show dockable panel
- `RcaDockablePanelViewModel.cs` - Main UI view model
- `PythonExecutionService.cs` - IronPython script execution
- `ServiceContainer.cs` - Dependency injection container

## Dependencies (Auto-Resolved)

### NuGet Packages
- `IronPython 3.4.2` - Python scripting engine
- `IronPython.StdLib 3.4.2` - Python standard library
- `DynamicLanguageRuntime 1.3.5` - DLR for Python
- `NUnit 3.13.3` - Unit testing framework
- `FluentAssertions 6.12.0` - Test assertions
- `NSubstitute 5.1.0` - Mocking framework

### Revit APIs
- `RevitAPI.dll` - Located in `libs/Revit/2026/RevitAPI.dll`
- `RevitAPIUI.dll` - Located in `libs/Revit/2026/RevitAPIUI.dll`

## Common Issues and Limitations

### Linux/macOS Development
**DO NOT ATTEMPT** - Project targets `net8.0-windows` exclusively. Error message:
```
error NETSDK1100: To build a project targeting Windows on this operating system, set the EnableWindowsTargeting property to true
```

### Missing Revit Installation
Build fails if Revit 2026 not installed to default location. Install Revit first.

### IronPython Deployment
Build automatically copies IronPython libraries to output. If missing, check NuGet restore.

## GitHub Actions CI/CD

- **Compile.yml**: Builds on `windows-2022` runners only
- **PublishRelease.yml**: Creates releases using NUKE build system
- Both use .NET 8 SDK and cached NuGet packages

## Coding Conventions

Follow these rules (enforced by `dotnet format`):

1. Use PascalCase for all class, method, and property names
2. Use camelCase for all private fields and local variables  
3. Add XML doc comments (`///`) above every public class and method
4. Keep each method focused on a single responsibility
5. Name files to match the primary class they contain
6. Organize code into folders by feature (e.g., `Commands`, `UI`, `Models`)
7. Use `using` directives only for namespaces you reference
8. Avoid magic strings—define all literal strings as `const` or resource entries
9. Encapsulate long event handlers by extracting helper methods
10. Always check for `null` before accessing object members
11. Use `TaskDialog.Show` inside a dedicated helper to display messages
12. Group related properties in region blocks with clear names
13. Declare all Revit API calls inside `try`/`catch` and log exceptions
14. Write unit tests for all non-UI logic in a separate test project
15. Reference .NET 8 SDK and target `net8.0-windows` in all project files
16. Commit small, focused changes with clear commit messages
17. Keep XAML markup minimal; define styles and resources externally
18. Use dependency injection for services and providers
19. Name boolean parameters or properties with "Is" or "Has" prefixes
20. Run `dotnet format` before each commit to enforce code style

## Quick Reference Commands

```powershell
# Bootstrap new development environment
git clone https://github.com/baidakovil/rca-plugin.git
cd rca-plugin
dotnet restore  # 2-5 minutes
dotnet build    # 5-10 minutes

# Daily development workflow  
dotnet build                              # 2-5 minutes
dotnet test --filter Category=Unit       # 2-5 minutes  
dotnet format --verify-no-changes        # 1-2 minutes

# Before committing
dotnet format rca-plugin.sln             # 1-3 minutes
dotnet test rca-plugin.sln               # 5-10 minutes
```