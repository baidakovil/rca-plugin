# RCA Hot-Reloading System

This directory contains the implementation of the RCA hot-reloading system, which allows dynamic updating of assemblies within Revit without requiring a full restart.

## Documentation Structure

The hot-reloading system is documented in three complementary files:

1. **[HOT-RELOADING.md](HOT-RELOADING.md)** - Comprehensive component documentation and code paths
   - System components and responsibilities
   - Code paths for different update scenarios
   - Implementation notes and configuration details

2. **[REVIT-INTEGRATION.md](REVIT-INTEGRATION.md)** - Detailed Revit startup and integration information
   - Complete Revit startup sequence
   - Two-phase initialization pattern
   - Thread synchronization with Revit UI
   - JSON state persistence between sessions

3. **[DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md)** - Practical guide for daily development
   - Quick reference for common scenarios
   - Development workflows
   - Debugging tips and common solutions
   - Hot-reload limitations

## Key Components

The hot-reloading system consists of several interconnected components:

- **AssemblyManagement**: Core tracking and monitoring components
  - `AssemblyInfo.cs`, `LoadedAssembliesInfo.cs`, `SignalInfo.cs`
  - `AssemblyStatusManager.cs` - Central tracking service

- **UI Components** (DEBUG only): Status visualization
  - `RibbonStatusDisplay.cs` - Thread-safe UI updates
  - Enhanced `RibbonService.cs` - Debug panel integration

- **Restart Mechanism**: Handles Revit restart for loader updates
  - `RestartManager.cs` - Manages restart workflow
  - `RestartRevitGraceful.ps1` - PowerShell restart script

- **Command Integration**: User and IPC interfaces
  - Enhanced `ReloadRuntimeCommand.cs` - User-facing control
  - Enhanced `RuntimeCommandHandler.cs` - IPC command handling

## Quick Start

To use the hot-reloading system during development:

1. Build your changes to the Runtime project
2. In Revit, click "Reload Runtime" in the RCA ribbon
3. If loader components need updating, follow the restart prompts

See [DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md) for detailed workflows.

## Architecture Diagram

```
┌────────────────────┐
│     Revit API      │◄────────┐
└────────────────────┘         │
         ▲                     │
         │                     │
┌────────┴─────────┐   ┌──────────────────┐
│   RCA Loader     │◄──┤AssemblyStatusMgr │
│ (Fixed Component)│   │  (State Tracker) │
└────────┬─────────┘   └──────┬───────────┘
         │                    │
         │                    │
         ▼                    ▼
┌─────────────────┐    ┌─────────────────┐
│   RCA Runtime   │    │  RestartManager │
│(Dynamic Component)   │(Graceful Restart)│
└─────────────────┘    └─────────────────┘
```

This system balances the need for stability (fixed loader components) with the flexibility of hot-reloading (dynamic runtime components) to optimize the developer experience while maintaining plugin reliability.
