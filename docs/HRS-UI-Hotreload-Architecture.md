# Dockable Panel UI Hot-Reload Architecture

This document describes the simplified architecture for hot-reloading the dockable panel UI in the RCA plugin.

## Problem Statement

The Loader and Runtime run in different AssemblyLoadContexts:
- **Loader** - Non-collectible context (persistent across reloads)
- **Runtime** - Collectible context (unloaded/reloaded during hot-reload)

This creates challenges for sharing UI components and services between the two contexts.

## Solution: SharedServiceRegistry Pattern

### Architecture Overview

```
┌──────────────────────────────────────────┐
│  Loader (Non-Collectible Context)       │
│  ┌────────────────────────────────────┐  │
│  │  SharedServiceRegistry (static)    │  │
│  │  Lives in Rca.Loader.Contracts    │  │
│  │  - Thread-safe registry           │  │
│  │  - Accessible from both contexts  │  │
│  └────────────────────────────────────┘  │
│  ┌────────────────────────────────────┐  │
│  │  DockablePanelHost                │  │
│  │  - Placeholder UI                  │  │
│  │  - SetContent(FrameworkElement)    │  │
│  └────────────────────────────────────┘  │
└──────────────────────────────────────────┘
                   ↕
        (Shared via merged Rca.Loader.dll)
                   ↕
┌──────────────────────────────────────────┐
│  Runtime (Collectible Context)          │
│  ┌────────────────────────────────────┐  │
│  │  RuntimeEntry.RegisterServices()   │  │
│  │  Registers to SharedRegistry:      │  │
│  │  - IPythonExecutionService         │  │
│  │  - IRevitContext                   │  │
│  │  - IRuntimePanelFactory            │  │
│  └────────────────────────────────────┘  │
│  ┌────────────────────────────────────┐  │
│  │  RuntimePanelFactory               │  │
│  │  CreatePanel():                    │  │
│  │  1. Resolve dependencies           │  │
│  │  2. Create RcaDockablePanel        │  │
│  │  3. Return FrameworkElement        │  │
│  └────────────────────────────────────┘  │
└──────────────────────────────────────────┘
```

### Key Components

#### 1. SharedServiceRegistry (`src/Rca.Loader.Contracts/SharedServiceRegistry.cs`)
- **Purpose**: Cross-context service registry
- **Location**: Lives in non-collectible Loader.Contracts namespace
- **Access**: Available to both Loader and Runtime through merged `Rca.Loader.dll`
- **Thread Safety**: All operations are thread-safe using lock
- **Lifecycle**: Cleared on Runtime.Shutdown()

```csharp
// Runtime registers services
SharedServiceRegistry.Register<IRuntimePanelFactory>(factory);

// Loader resolves services
var factory = SharedServiceRegistry.Resolve<IRuntimePanelFactory>();
```

#### 2. RuntimePanelFactory (`src/Rca.Runtime/UI/RuntimePanelFactory.cs`)
- **Purpose**: Creates dockable panel UI with dependencies
- **Registered**: During `RuntimeEntry.Initialize()`
- **Dependencies**: Resolves from SharedServiceRegistry
- **Returns**: `RcaDockablePanel` as `FrameworkElement`

#### 3. DockablePanelHost (`src/Rca.Loader/UI/DockablePanelHost.cs`)
- **Purpose**: Placeholder container in Loader
- **Lifecycle**: Created once during Loader startup
- **Content**: Replaced via `SetContent()` after Runtime loads
- **Thread Safety**: Uses Dispatcher for UI thread marshalling

### Hot-Reload Flow

1. **Loader Startup** (`LoaderApp.OnStartup`)
   - Create `DockablePanelHost` with placeholder
   - Register dockable pane with Revit
   - Store `PanelHost` reference for later

2. **Runtime Load** (`RuntimeManager.ReloadRuntime`)
   - Load Runtime assembly into collectible context
   - Create RuntimeEntry instance
   - Call `RuntimeEntry.Initialize()`

3. **Service Registration** (`RuntimeEntry.RegisterServices`)
   - Create `RuntimePanelFactory`
   - Register in `SharedServiceRegistry`
   - Also register dependencies (PythonService, RevitContext)

4. **UI Creation** (`RuntimeManager.CreateRuntimeDockableContent`)
   - Resolve `IRuntimePanelFactory` from SharedRegistry
   - Call `factory.CreatePanel()`
   - Return `FrameworkElement`

5. **UI Injection** (`ReloadRuntimeCommand.TryReplaceDockableContent`)
   - Get `PanelHost` from LoaderApp
   - Create UI via RuntimeManager
   - Call `PanelHost.SetContent(element)`
   - Show dockable pane

6. **Runtime Unload** (`RuntimeManager.UnloadRuntime`)
   - Call `RuntimeEntry.Shutdown()`
   - Clear SharedServiceRegistry
   - Clear panel host content
   - Unload collectible context

### Why This Works

1. **SharedServiceRegistry is static** - Single instance shared across contexts
2. **Lives in non-collectible Loader.Contracts** - Survives Runtime reload
3. **Type identity preserved** - Interfaces defined in Loader.Contracts are same for both contexts
4. **No reflection fallbacks needed** - Factory pattern is reliable and simple

### Design Principles

1. **Single responsibility** - Each component has one clear purpose
2. **Fail fast** - Missing factory returns null immediately
3. **Minimal logging** - Only log significant events, not every step
4. **No fallbacks** - If factory pattern fails, it's a real error
5. **Thread safe** - All cross-context operations use locks

### Files Modified

- `src/Rca.Loader.Contracts/SharedServiceRegistry.cs` (new)
- `src/Rca.Loader.Contracts/IRuntimePanelFactory.cs` (existing interface)
- `src/Rca.Runtime/UI/RuntimePanelFactory.cs` (new)
- `src/Rca.Runtime/RuntimeEntry.cs` (modified to register in SharedRegistry)
- `src/Rca.Loader/Services/RuntimeManager.cs` (simplified to use SharedRegistry)
- `src/Rca.Loader/LoaderApp.cs` (removed early pane.Show())
- `src/Rca.Loader/Commands/ReloadRuntimeCommand.cs` (show pane after content load)

### Troubleshooting

**UI not appearing:**
- Check logs for "IRuntimePanelFactory not registered"
- Verify RuntimeEntry.Initialize() was called
- Confirm SharedServiceRegistry.Resolve() returns non-null

**Dockable pane error on startup:**
- Ensure pane.Show() is NOT called in LoaderApp.OnStartup()
- Show pane only after content is loaded

**Services not found:**
- All runtime services must be registered in SharedServiceRegistry, not local ServiceContainer
- SharedServiceRegistry is the source of truth for cross-context services
