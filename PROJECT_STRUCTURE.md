# RCA Plugin - Multi-Project Structure

This repository has been restructured into multiple projects for better organization and maintainability.

## Project Structure

### Core Projects

- **Rca.Contracts** (`src/Rca.Contracts/`)
  - Contains interfaces and contracts
  - Target Framework: .NET 8.0
  - Purpose: Define abstractions for cross-project communication

- **Rca.Core** (`src/Rca.Core/`)
  - Contains core business logic, services, and helpers
  - Target Framework: .NET 8.0
  - Dependencies: Rca.Contracts, IronPython packages, Revit API
  - Purpose: Core functionality, Python execution, debugging services

- **Rca.UI** (`src/Rca.UI/`)
  - Contains ViewModels, Views, and UI components
  - Target Framework: .NET 8.0-windows (WPF)
  - Dependencies: Rca.Contracts, Rca.Core, Revit API
  - Purpose: User interface components and view models

- **Rca.Network** (`src/Rca.Network/`)
  - Placeholder for future network functionality
  - Target Framework: .NET 8.0
  - Dependencies: Rca.Contracts
  - Purpose: Reserved for future network-related features

- **RcaPlugin** (`src/RcaPlugin/`)
  - Main plugin entry point
  - Target Framework: .NET 8.0-windows (WPF)
  - Dependencies: All other projects, Revit API
  - Purpose: Main Revit plugin assembly with commands and entry point

### Build Projects

- **Build** (`build/`)
  - Build automation project (NUKE build system)
  - References the main RcaPlugin project
  - Purpose: Automated builds, packaging, and deployment

- **Installer** (`install/`)
  - MSI installer project
  - Purpose: Create installation packages

## Dependencies Flow

```
RcaPlugin (main entry)
├── Rca.UI (user interface)
│   ├── Rca.Core (business logic)
│   │   └── Rca.Contracts (interfaces)
│   └── Rca.Contracts
├── Rca.Core
│   └── Rca.Contracts
├── Rca.Network (future)
│   └── Rca.Contracts
└── Rca.Contracts
```

## Building

All projects target .NET 8.0 and can be built using:

```bash
dotnet build rca-plugin.sln
```

The main plugin output will be in `src/RcaPlugin/bin/`.

## Key Changes from Original Structure

1. **Separation of Concerns**: Code is now organized by responsibility
2. **Better Testability**: Core logic is separated from UI and Revit dependencies
3. **Maintainability**: Clear project boundaries and dependencies
4. **Future Extensibility**: Network project ready for future features
5. **Namespace Organization**: Each project has its own namespace
   - `Rca.Contracts` - Interfaces
   - `Rca.Core.Services` - Services
   - `Rca.Core.Helpers` - Helper classes
   - `Rca.UI.ViewModels` - View models
   - `Rca.UI.Views` - UI components
   - `RcaPlugin` - Main plugin (kept for compatibility)