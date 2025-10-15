# Hash-based Hot-reload System (AI-first Documentation)

This document explains the current source-hash based system used by the RCA hot-reload pipeline.

---

## Goals
- Produce deterministic source-based hashes for two distinct concerns:
  - runtime group — represents the authoritative source state for `Rca.Runtime` and projects merged into the runtime output
  - loader group — represents the authoritative source state for Loader components (`Rca.Loader`, `Rca.Loader.Contracts`, shared logging contracts)
- Make the set of source roots explicit and configurable via MSBuild.
- Use the hash consistently as assembly metadata and for change detection at runtime.

---

## Top-level files (where to look)
- Source hash generator tool
  - `src/Tools/SourceHashGenerator/Program.cs` — CLI tool that computes SHA256 over normalized source content. Accepts `--roots` (semicolon-separated) and `--out`.

- MSBuild orchestration (single place)
  - `Directory.Build.targets`
    - Computes both hashes once per build, serialized via a global mutex and a per-build TEMP lock to avoid duplicate work when projects build in parallel
    - Overwrites two artifacts on each build: `build/artifacts/hashes/loader.txt` and `build/artifacts/hashes/runtime.txt`
    - Injects assembly metadata into all participating projects by generating `Rca.AssemblyMetadata.g.cs` with:
      - `AssemblyMetadata("SourceHash", "<hash>")`
      - `AssemblyMetadata("DeployFolder", "<timestamp>")`
    - Creates deploy marker files after build for traceability:
      - Runtime: `%LOCALAPPDATA%/RCA/Runtime/<timestamp>/SourceHash-Runtime-<hash>.txt`
      - Loader:  `%LOCALAPPDATA%/RCA/Runtime/<timestamp>/SourceHash-Loader-<hash>.txt`

- Runtime/Loader consumers
  - `src/Rca.Loader/AssemblyManagement/AssemblyStatusManager.cs`
    - Reads `SourceHash` from DLL assembly metadata on disk (never from in-memory assemblies)
    - Detects Runtime updates by comparing discovered hash in latest deploy folder with loaded runtime hash
    - Detects Loader updates by comparing installed addin hash with latest Loader group hash in the latest deploy folder
  - `src/Rca.Loader/Restart/RestartManager.cs`
    - Uses metadata-based validation for copy operations (no dependency on external hash files). Launches PowerShell script to restart Revit.

- Constants
  - `src/Rca.Loader/Infrastructure/LoaderConstants.cs` — deploy locations and filenames for loader and runtime groups.

---

## How hashes are calculated (generator behavior)
- The generator scans provided roots recursively.
- File selection: `.cs, .csproj, .props, .targets, .xaml, .resx, .json, .tt` (text files normalized to LF before hashing).
- Ignores directories: `bin, obj, .git, .vs, node_modules, packages`.
- Files are ordered deterministically and deduplicated before hashing.
- SHA256 is computed incrementally and written to the `--out` file.

CLI usage examples:
- `dotnet run --project src/Tools/SourceHashGenerator -- --roots "src/Rca.Runtime;src/Rca.Core;src/Rca.UI" --out "<outpath>"`

When invoked from MSBuild, the exe is called directly by `Directory.Build.targets`.

---

## What projects are included in each hash (current configuration)
- Runtime group (configured in `Directory.Build.targets` property `RcaRuntimeRoots`):
  - `src/Rca.Runtime`
  - `src/Rca.Core`
  - `src/Rca.Network`
  - `src/Rca.UI`
  - `src/Rca.Contracts`

- Loader group (configured in `Directory.Build.targets` property `RcaLoaderRoots`):
  - `src/Rca.Loader`
  - `src/Rca.Loader.Contracts`
  - `src/Rca.Logging.Contracts`

Change these semicolon-separated root lists in `Directory.Build.targets` to adjust coverage.

---

## Build-time integration summary
- Hash computation is executed exactly once per build (both groups), regardless of how many projects are compiled:
  - Global named mutex serializes concurrent invocations
  - TEMP-based lock keyed by the build stamp (`RcaHotReloadTimestamp`) prevents duplicate work across projects
- Two files are produced and always overwritten: `build/artifacts/hashes/loader.txt` and `build/artifacts/hashes/runtime.txt`.
- The selected group hash is embedded into assemblies via `AssemblyMetadata("SourceHash", ...)` for downstream detection.

---

## Runtime behavior (startup & detection)
1. On loader startup, `AssemblyStatusManager.InitializeOnStartup()` reads metadata from disk:
   - Loader: read `SourceHash` from the installed addin DLL(s) in `%APPDATA%/Autodesk/Revit/Addins/2026/Rca`
   - Runtime (discovered): read `SourceHash` values from DLLs in the latest `%LOCALAPPDATA%/RCA/Runtime/<timestamp>` directory and ensure group consistency
   - Loaded runtime state is empty until a reload occurs
2. On `BUILD_COMPLETED` or manual check, `ProcessMsBuildSignal(latest)` updates discovered hashes and classifies the event:
   - `only loader outdated`, `only runtime outdated`, `both loader and runtime outdated`, or `no changes`
3. On successful runtime reload, `UpdateHashesAfterReload` updates the “loaded runtime” hash to match the discovered one.

---

## Restart/validate flow
- `RestartManager.ValidateAssemblyCopy(source, target)` compares `SourceHash` assembly metadata between source and target DLLs to confirm a correct copy.
- Revit restart is orchestrated by an external PowerShell script launched from the addin. In current DEBUG flow the script path is resolved by the addin (see `RestartManager`).

---

## Developer / CI suggestions
- CI should run `dotnet build` for the solution to produce deployable runtime folders. The two build artifacts in `build/artifacts/hashes` are overwritten on every build.
- Keep `RcaRuntimeRoots`/`RcaLoaderRoots` in `Directory.Build.targets` in sync with the projects that contribute to the respective outputs.
- Prefer reading `SourceHash` from assemblies on disk for any tooling that needs to detect changes.

---

## How to test/verify locally
1. Run a full build: `dotnet build --no-incremental`
2. Confirm the two files updated: `build/artifacts/hashes/runtime.txt`, `build/artifacts/hashes/loader.txt`
3. Start Revit with RCA loaded; build the solution to trigger `BUILD_COMPLETED`
4. Watch the addin’s status lines and logs for change classification and (if needed) dialogs

---

## Quick links (files you will likely inspect)
- Generator: `src/Tools/SourceHashGenerator/Program.cs`
- MSBuild integration: `Directory.Build.targets` (hash generation, metadata injection, deploy markers, notification)
- Status tracking: `src/Rca.Loader/AssemblyManagement/AssemblyStatusManager.cs`
- Restart: `src/Rca.Loader/Restart/RestartManager.cs`
- Loader constants: `src/Rca.Loader/Infrastructure/LoaderConstants.cs`
