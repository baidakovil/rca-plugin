DOCKABLE PANEL: Integration and Hot-Reload

Overview
--------
This document describes how the Loader registers a DockablePane in Revit at startup and how the Runtime replaces the placeholder content after being loaded.

Key points
----------
- The Loader registers a minimal `DockablePanelHost` at Revit startup. This host implements `IRuntimePanelHost` and exposes `SetContent(FrameworkElement?)`.
- The Loader loads `Rca.Runtime.dll` on demand and requests the runtime UI content via `IRuntimePanelFactory` from `ServiceContainer.Instance`.
- If the runtime does not register `IRuntimePanelFactory`, the Loader attempts a minimal fallback: instantiate `RcaDockablePanel` via parameterless constructor from the runtime assembly.

How to implement runtime factory
--------------------------------
In `Rca.Runtime` project add the following in `RuntimeEntry.Initialize()`:

```csharp
// Example runtime factory registration
ServiceContainer.Instance.Register<IRuntimePanelFactory>(new RuntimePanelFactory());

public class RuntimePanelFactory : IRuntimePanelFactory
{
    public FrameworkElement? CreatePanel()
    {
        // Ensure to provide a constructor that does not capture loader types
        return new RcaDockablePanel(
            () => { /* provide UIApplication if available */ return null; },
            /* IPythonExecutionService implementation */ null!,
            () => new DebugInfoWindow(/* IDebugLogService implementation */ null!));
    }
}
```

Important notes
---------------
- Prefer registering a factory rather than relying on reflection. This avoids brittle string-based type names and is resilient to repack/merge.
- Ensure `IRuntimePanelFactory` and `ServiceContainer` are in shared assemblies loaded into the default context.
- Before unloading runtime, Loader calls `host.SetContent(null)` to clear visual references so the runtime ALC can unload.

Troubleshooting
---------------
- If the panel does not appear after reload: verify runtime registered `IRuntimePanelFactory` or provided parameterless constructor on `RcaDockablePanel`.
- If the runtime doesn't unload: ensure no static references or event handlers hold runtime instances. Clear panel content before unloading.

GUIDs and persistence
---------------------
The Loader uses a fixed DockablePaneId GUID to ensure Revit remembers panel placement between sessions.

