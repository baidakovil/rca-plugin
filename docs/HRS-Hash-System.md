# Hash-based Hot-reload System (AI-first Documentation)

This document explains the current source-hash based system used by the RCA hot-reload pipeline.

---

## Goals
- Produce deterministic source-based hashes for two distinct concerns:
  - runtime group — authoritative source state for `Rca.Runtime` and related projects
  - loader group — authoritative source state for Loader components (`Rca.Loader`, `Rca.Loader.Contracts`, shared logging contracts)
- Make the set of source roots explicit and configurable via MSBuild.
- Use the hash consistently as assembly metadata and for change detection at runtime.

---

## Top-level files (where to look)
- Source hash generator tool
  - `src/Tools/Rca.SourceHashGenerator/Program.cs` — CLI tool (`Rca.SourceHashGenerator`) that computes SHA256 over normalized source content and writes a deploy marker file `SourceHash-<Group>-<shortHash>.txt` in the deploy timestamp folder. It only writes a fixed `--out` file when explicitly requested by the caller.

- MSBuild orchestration (single place)
  - `Directory.Build.targets` and `build/targets/hash-generation.targets`
    - `ComputeSourceHashes` target computes both hashes once per build
    - Invokes generator to write marker files (`SourceHash-Loader-<hash>.txt`, `SourceHash-Runtime-<hash>.txt`) into the deploy timestamp folder
    - `AddHashMarkersToAdditionalFiles` target adds marker files to `AdditionalFiles` for Source Generator access
  - `src/Tools/Rca.BuildMetadata.Generator` (Roslyn Source Generator)
    - Reads marker files from `AdditionalFiles` to extract hashes
    - Generates `Rca.AssemblyMetadata.g.cs` containing:
      - `AssemblyMetadata("SourceHash", "<hash>")`
      - `AssemblyMetadata("DeployFolder", "<timestamp>")`

- Runtime/Loader consumers
  - `src/Rca.Loader/AssemblyManagement/AssemblyStatusManager.cs`
    - Reads `SourceHash` from DLL assembly metadata on disk
    - Detects runtime/loader updates by comparing embedded metadata values
  - `src/Rca.Loader/Restart/RestartManager.cs`
    - Uses embedded metadata for validation during copy/restart operations

- Constants
  - `src/Rca.Loader/Infrastructure/LoaderConstants.cs` — deploy locations and filenames for loader and runtime groups.

---

## How hashes are calculated (generator behavior)
- The generator scans provided roots recursively.
- File selection: `.cs, .csproj, .props, .targets, .xaml, .resx, .json, .tt` (text files normalized to LF before hashing).
- Ignores directories: `bin, obj, .git, .vs, node_modules, packages`.
- Files are ordered deterministically and deduplicated before hashing.
- SHA256 is computed incrementally and a short hex prefix is used for human-friendly marker filenames (e.g., `c06c76`).
- The generator writes a marker file into the deploy timestamp folder named `SourceHash-<Group>-<shortHash>.txt`. A Roslyn Source Generator (`src/Tools/Rca.BuildMetadata.Generator`) reads these marker files via `AdditionalFiles` and embeds `SourceHash` and `DeployFolder` as assembly metadata into compiled assemblies. The same Source Generator also emits `Rca.Generated.RcaBuildMetadata` class in `Rca.Contracts` to expose build-time constants (e.g., `SourceHashLength`); physical generator outputs are not written into source tree.

Rationale for marker filenames:
- Embedding the short hash in the filename gives a clear, auditable trace of what was deployed in a given timestamp folder. It helps correlate deployed binaries with hashes without opening files or inspecting assembly metadata.

---

## What projects are included in each hash (current configuration)
- Runtime group (`RcaRuntimeRoots` in `build/props/paths.props`):
  - `src/Rca.Runtime`
  - `src/Rca.Core`
  - `src/Rca.Network`
  - `src/Rca.UI`
  - `src/Rca.Contracts`

- Loader group (`RcaLoaderRoots` in `build/props/paths.props`):
  - `src/Rca.Loader`
  - `src/Rca.Loader.Contracts`
  - `src/Rca.Logging.Contracts`

Change these semicolon-separated root lists in `build/props/paths.props` to adjust hash coverage.
Project name groupings (for MSBuild conditions and Source Generator) are defined separately in `build/props/project-groups.props`.

---

## Build-time integration summary
- Hash computation is executed once per build for both groups via `ComputeSourceHashes` target; a global mutex in `EnsureRcaStamp.ps1` ensures single-writer semantics when creating/updating the timestamp file.
- The generator creates marker files (`SourceHash-Loader-<hash>.txt`, `SourceHash-Runtime-<hash>.txt`) in the deploy timestamp folder.
- Marker files are added to `AdditionalFiles` by MSBuild target `AddHashMarkersToAdditionalFiles`, enabling Source Generator to read them.
- Source Generator (`Rca.BuildMetadata.Generator`) reads marker files, extracts hashes, and embeds `SourceHash` and `DeployFolder` as assembly metadata into compiled assemblies so runtime consumers can read them directly from DLLs.

---

## Runtime behavior (startup & detection)
1. On loader startup, `AssemblyStatusManager.InitializeOnStartup()` reads metadata from disk:
   - Loader: read `SourceHash` from the installed addin DLL(s) in the Addins folder
   - Runtime (discovered): read `SourceHash` values from DLLs in the latest `%APPDATA%\Autodesk\Revit\Addins\$(RcaRevitVersion)\<timestamp>` directory and ensure group consistency
   - Loaded runtime state is established after a successful runtime load/reload and reflects the embedded metadata of the loaded assemblies
2. On `BUILD_COMPLETED` or manual check, `ProcessMsBuildSignal(latest)` updates discovered hashes and classifies the event as:
   - `only loader outdated`, `only runtime outdated`, `both loader and runtime outdated`, or `no changes`
3. On successful runtime reload, `UpdateHashesAfterReload` updates the "loaded runtime" hash to match the discovered one.

---

## Restart/validate flow
- `RestartManager.ValidateAssemblyCopy(source, target)` compares `SourceHash` assembly metadata between source and target DLLs to confirm a correct copy.
- Restart and copy operations rely on assembly metadata rather than external sidecar files, which improves robustness when files are copied by external processes.

---

## Developer / CI suggestions
- CI should run `dotnet build --no-incremental` for the solution to produce deployable runtime folders and marker files.
- Keep `RcaRuntimeRoots`/`RcaLoaderRoots` in `build/props/paths.props` in sync with projects that contribute to each group.
- Prefer reading `SourceHash` from assemblies on disk for any tooling that needs to detect changes.

---

## How to test/verify locally
1. Run a full build: `dotnet build --no-incremental`
2. Confirm the latest folder under `%APPDATA%\Autodesk\Revit\Addins\$(RcaRevitVersion)\<timestamp>` contains marker files `SourceHash-Loader-<hash>.txt` and `SourceHash-Runtime-<hash>.txt` alongside deployed DLLs.
3. Start Revit with the RCA addin loaded; build the solution to trigger `BUILD_COMPLETED` and verify the addin reacts and classifies changes correctly.

---

## Quick links (files you will likely inspect)
- Generator: `src/Tools/Rca.SourceHashGenerator/Program.cs`
- MSBuild integration: `build/targets/hash-generation.targets` (hash computation and marker file creation), `src/Tools/Rca.BuildMetadata.Generator/BuildMetadataGenerator.cs` (Source Generator for assembly metadata)
- Status tracking: `src/Rca.Loader/AssemblyManagement/AssemblyStatusManager.cs`
- Restart: `src/Rca.Loader/Restart/RestartManager.cs`
- Loader constants: `src/Rca.Loader/Infrastructure/LoaderConstants.cs`

---

End of document.
