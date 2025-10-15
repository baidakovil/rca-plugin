# Hot-Reload System (HRS)

This document describes the Revit Chat Assistant (RCA) Hot-Reload System architecture, implementation, and operational behavior.

---

## Objectives

- Hot-reload Runtime without restarting Revit, preserving UI state and user context.
- Rebuild/Reload Loader safely via Revit restart orchestrated by the HRS.
- Track versions of Loader and Runtime assemblies using source-code hashes embedded at build time.
- Notify users interactively when new builds are available, giving them control over when to apply updates.

---

## Architecture overview

The HRS consists of:

1. Build-time hash generator and MSBuild targets that compute two group hashes once per build and embed them into assemblies.
2. MSBuild integration that deploys to timestamped folders and sends a lightweight notification.
3. Named Pipe protocol for MSBuild-to-Revit communication.
4. AssemblyStatusManager that tracks installed (Loader), discovered (Runtime), and loaded (Runtime) versions using `SourceHash` assembly metadata.
5. Interactive dialogs for reload/restart actions.
6. RuntimeManager that performs collectible assembly loading for hot-reload of UI and services.

---

## Build signal flow (current implementation)

```
MSBuild Build                    Revit Addin
    |                                |
    |-- Deploy DLLs to folder -->    |
    |                                |
    |-- Send BUILD_COMPLETED -->     |
    |                                |
                                     |-- Find latest folder
                                     |-- Read SourceHash from DLLs on disk
                                     |-- Compare with loaded
                                     |-- Show user dialog
                                     |     - Reload Runtime
                                     |     - Restart Revit (if Loader outdated)
                                     |     - Ignore
```

Benefits:
- No complex payloads; addin is the source of truth for discovery
- Stable detection (metadata read from DLLs on disk)
- Clear user control and safe restart path for Loader updates

---

## Build-time integration

- `Directory.Build.targets` orchestrates hash computation and metadata injection:
  - Computes runtime and loader hashes once per build (mutex + TEMP lock)
  - Overwrites `build/artifacts/hashes/runtime.txt` and `build/artifacts/hashes/loader.txt`
  - Emits `Rca.AssemblyMetadata.g.cs` with `[assembly: AssemblyMetadata("SourceHash", ...)]` and `[assembly: AssemblyMetadata("DeployFolder", ...)]`
  - Copies Runtime/Loader DLLs into `%LOCALAPPDATA%/RCA/Runtime/<timestamp>` and writes marker files for traceability

---

## Change detection at runtime

- Loader updates: compare installed addin hash vs latest loader group hash discovered in `%LOCALAPPDATA%/RCA/Runtime/<timestamp>`
- Runtime updates: compare discovered runtime group hash vs loaded runtime hash
- Events: `only loader outdated`, `only runtime outdated`, `both loader and runtime outdated`, `no changes`

---

## Restart flow (Loader updates)

- The addin launches a PowerShell script to perform a graceful restart and copy updated assemblies into the Addins folder.
- In current DEBUG configuration the script path is resolved as an absolute path: `C:\Users\baidakov\rca-plugin\build\Scripts\RestartRevitGraceful.ps1`.
- Parameters passed:
  - `-SourcePath` — latest deploy folder
  - `-TargetPath` — `%APPDATA%/Autodesk/Revit/Addins/2026/Rca`
  - `-RevitExecutable` — full path to `Revit.exe`
  - Optional: `-FilePath` to open a Revit project after restart

---

## Dockable Panel UI Hot-Reload

- UI XAML is loaded deterministically from an embedded XAML manifest resource to avoid ALC/pack URI complications.
- `Rca.UI.RcaDockablePanel.xaml` is parsed at runtime via `XamlReader.Parse` after removing `x:Class`.
- Runtime loads into a collectible context and injects content into the persistent `DockablePanelHost`.

---

## Named Pipe protocol

- Pipe name: `RCA_PIPE`, JSON line protocol
- Commands: `BUILD_COMPLETED`, `RELOAD_RUNTIME`, `STATUS`, `RUN_TESTS`, `TEST_INIT`
- `BUILD_COMPLETED` triggers discovery and user dialog via `ExternalEvent`

---

## How to test/verify locally

1. Build solution: `dotnet build --no-incremental`
2. Confirm two hash artifacts updated in `build/artifacts/hashes`
3. Start Revit with RCA
4. Build again; observe `BUILD_COMPLETED` handling and dialog
5. For Loader updates choose Restart; the PowerShell script will close Revit, copy assemblies, and start Revit again

---

## Where to change/extend

- Hash roots and orchestration: `Directory.Build.targets`
- Detection logic: `AssemblyStatusManager`
- Restart behavior: `RestartManager`
- UI hot-reload: `Rca.UI.Views.RcaDockablePanel`
