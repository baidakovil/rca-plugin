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

1. Build-time hash generator and MSBuild targets that compute two group hashes once per build and embed them into assemblies as assembly metadata.
2. MSBuild integration that deploys build outputs into timestamped folders and records marker files for traceability.
3. Named Pipe protocol for MSBuild-to-Revit communication.
4. `AssemblyStatusManager` that tracks installed (Loader), discovered (Runtime), and loaded (Runtime) versions using `SourceHash` assembly metadata.
5. Interactive dialogs for reload/restart actions.
6. `RuntimeManager` that performs collectible assembly loading for hot-reload of UI and services.

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
- The add-in reads canonical metadata from deployed DLLs on disk (single source of truth).
- Detection is robust and deterministic because hashes are embedded into assemblies at build time.
- Users retain control when updates are applied; Loader updates use a safe restart flow.

---

## Build-time integration

- `Directory.Build.targets` orchestrates hash computation and metadata injection and deploys build outputs into a timestamped deploy folder under the Revit Addins root (default: `%APPDATA%\Autodesk\Revit\Addins\$(RcaRevitVersion)\<timestamp>`).
- The source-hash generator tool (`src/Tools/SourceHashGenerator`) computes a group hash and creates a marker file inside the deploy timestamp folder named `SourceHash-<Group>-<shortHash>.txt` (for example `SourceHash-Runtime-c06c76.txt`).
- The generator no longer creates a fixed-name duplicate file by default; it writes an explicit `--out` file only if MSBuild or a caller requests it.
- The MSBuild integration intentionally invokes the generator to produce only marker files in the deploy folder. The build also generates a small source file `Rca.AssemblyMetadata.g.cs` containing `[assembly: AssemblyMetadata("SourceHash", "<hash>")]` and `[assembly: AssemblyMetadata("DeployFolder", "<timestamp>")]` so the hash and deploy timestamp are embedded into every participating assembly.
- The MSBuild integration intentionally invokes the generator to produce only marker files in the deploy folder. The build also embeds assembly metadata (`SourceHash` and `DeployFolder`) so the hash and deploy timestamp are included in compiled assemblies. A Roslyn Source Generator (`src/Tools/Rca.BuildMetadata.Generator`) is wired as an analyzer for `Rca.Contracts` (and the integration test project) to emit `Rca.Generated.RcaBuildMetadata` at compile time; physical generator outputs are not written into source tree.

Rationale:
- Marker files carrying the short hash in their filename provide a single, human-friendly trace of what exactly was deployed at that timestamp and eliminate ambiguity between multiple builds.
- Embedding the hash into assemblies guarantees the runtime and loader can validate copies and detect mismatches without relying on external sidecar files.

---

## Change detection at runtime

- Loader updates: compare installed addin hash vs latest loader group hash discovered in the latest deploy folder under the Revit Addins root: `%APPDATA%\Autodesk\Revit\Addins\$(RcaRevitVersion)\<timestamp>`.
- Runtime updates: compare discovered runtime group hash (from the marker file / discovered assemblies) vs loaded runtime hash (embedded in the loaded runtime assemblies).
- Classification events produced: `only loader outdated`, `only runtime outdated`, `both loader and runtime outdated`, `no changes`.

---

## Restart flow (Loader updates)

- When a Loader update is required in production, the addin performs a validated deploy and orchestrates a graceful restart of Revit. Validation relies on assembly metadata (`SourceHash`) embedded in the assemblies; the addin compares metadata on source and deployed files to confirm a correct deploy.
- In the developer hot‑reload workflow the build now deploys both Loader and Runtime into a timestamped folder under the Revit Addins root and the `.addin` manifest references the Loader assembly relative to that timestamp folder. The developer restart helper (`build\Scripts\RestartRevitGraceful.ps1`) is simplified and no longer copies files; it only restarts Revit because MSBuild has already deployed artifacts to the timestamped Addins folder. CI/packaging should still validate metadata when moving assemblies into final installation locations.

---

## Dockable Panel UI Hot-Reload

- UI XAML is loaded deterministically from an embedded XAML manifest resource to avoid ALC/pack URI complications.
- `Rca.UI.RcaDockablePanel.xaml` is parsed at runtime via `XamlReader.Parse` after removing `x:Class` to avoid type name conflicts when loading into collectible contexts.
- The runtime loads into a collectible `AssemblyLoadContext` and injects content into the persistent `DockablePanelHost` so the panel instance in Revit survives runtime reloads.

---

## Named Pipe protocol

- Pipe name: `RCA_PIPE`, JSON line protocol
- Commands: `BUILD_COMPLETED`, `RELOAD_RUNTIME`, `STATUS`, `RUN_TESTS`, `TEST_INIT`
- `BUILD_COMPLETED` triggers discovery and user dialog via `ExternalEvent`.

---

## How to test/verify locally

1. Build solution: `dotnet build --no-incremental` (this produces a timestamped deploy folder under the Revit Addins root and marker files).
2. Confirm deploy timestamp folder in `%APPDATA%\Autodesk\Revit\Addins\$(RcaRevitVersion)\<timestamp>` contains `SourceHash-Loader-<hash>.txt` and `SourceHash-Runtime-<hash>.txt` alongside deployed DLLs.
3. Start Revit with RCA loaded.
4. Build again; the addin will receive `BUILD_COMPLETED`, discover the latest folder, and classify changes; dialogs will appear according to classification.
5. For Loader updates choose Restart; in the developer workflow the helper script will only restart Revit after MSBuild deployed the timestamped Addins folder. Production installers should perform metadata validation during installation.

---

## Where to change/extend

- Hash roots and orchestration: `Directory.Build.targets`
- Detection logic: `AssemblyStatusManager`
- Restart behavior: `RestartManager`
- UI hot-reload: `Rca.UI.Views.RcaDockablePanel`

---

## Notes

- The design intentionally keeps marker files in the deploy timestamp folder and embeds metadata into assemblies so runtime detection is resilient and does not depend on ephemeral build artifacts located elsewhere.
