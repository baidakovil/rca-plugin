# Hash-based Hot-reload System (AI-first Documentation)

This document explains the new source-hash based system used by the RCA hot-reload pipeline. It is written for future AI agents and maintainers to quickly understand what was implemented, why, where to look in the repository and how to troubleshoot or extend the system.

---

## Goals
- Produce deterministic source-based hashes for two distinct concerns:
  - `source-hash.runtime.txt` — represents the authoritative source state for everything that is merged into `Rca.Runtime` at build/deploy time.
  - `source-hash.loader.txt` — represents the authoritative source state for the Loader components (the `Rca.Loader` and `Rca.Loader.Contracts` projects) when the loader is deployed.
- Make the set of source files included in each hash explicit and configurable via MSBuild.
- Make the generator deterministic and usable from MSBuild and CLI.

---

## Top-level files (where to look)
- Source hash generator tool
  - `src/Tools/SourceHashGenerator/Program.cs` — the CLI tool that computes SHA256 over normalized source content. It accepts either `--root` or `--roots` (semicolon/comma/pipe-separated) and `--out` for output.

- Runtime: MSBuild integration and produced runtime hash
  - `src/Rca.Runtime/Rca.Runtime.csproj` — `DeployRuntime` target runs the generator over _all_ projects that are merged into `Rca.Runtime` and writes `source-hash.runtime.txt` into the timestamped runtime deploy folder.
  - Output at build: `%LOCALAPPDATA%\RCA\Runtime\<timestamp>\source-hash.runtime.txt`

- Loader: MSBuild integration and produced loader hash (deployed to runtime temp folder only)
  - `src/Rca.Loader/Rca.Loader.csproj` — `DeployLoaderToTemp` target runs the generator over `Rca.Loader` and `Rca.Loader.Contracts` and writes `source-hash.loader.txt` into the same timestamped runtime deploy folder (not copied to the Revit addin folder).
  - Output at build (when loader deploy occurs): `%LOCALAPPDATA%\RCA\Runtime\<timestamp>\source-hash.loader.txt`

- Runtime/Loader consumers
  - `src/Rca.Loader/AssemblyManagement/AssemblyStatusManager.cs` — reads the hashes at startup and decides whether loader/runtime are outdated. It prefers:
    1. loader: read `source-hash.loader.txt` from loader assembly directory (addin) if present
    2. fallback loader: look at latest runtime deploy folder and runtime deploy root for `source-hash.loader.txt`
    3. runtime: read `source-hash.runtime.txt` from the latest runtime deploy folder; fallback to scanning runtime deploy root
    4. last resort: developer-mode computation from local repo structure
  - `src/Rca.Loader/Restart/RestartManager.cs` — validates loader copy using `source-hash.loader.txt` when present (fallback to binary compare if not).

- Constants
  - `src/Rca.Loader/Infrastructure/LoaderConstants.cs` — locations and filenames used by the loader (runtime deploy root, addin dir, filenames, JSON status path).

---

## How hashes are calculated (generator behavior)
- The generator scans provided root directories recursively.
- File selection is by extension list (default: `.cs, .csproj, .props, .targets, .xaml, .resx, .json, .tt`), and ignores these directories: `bin`, `obj`, `.git`, `.vs`, `node_modules`, `packages`.
- Non-binary text files are normalized: CRLF and CR are converted to LF (`\n`) before hashing.
- Files are ordered deterministically (by full path) and deduplicated before hashing.
- SHA256 is computed incrementally and written as a lowercase hex string to the `--out` file.

CLI usage examples:
- Single root:
  - `dotnet run --project src/Tools/SourceHashGenerator -- --root src/Rca.Runtime --out "<outpath>"`
- Multiple roots:
  - `dotnet run --project src/Tools/SourceHashGenerator -- --roots "src/Rca.Runtime;src/Rca.Core;src/Rca.UI" --out "<outpath>"`

When invoked from MSBuild, the projects call the built exe directly for performance and reproducibility.

---

## What projects are included in each hash (current configuration)
- `source-hash.runtime.txt` (configured in `src/Rca.Runtime/Rca.Runtime.csproj`):
  - `src/Rca.Runtime`
  - `src/Rca.Core`
  - `src/Rca.UI`
  - `src/Rca.Network`
  - `src/Rca.Contracts`

  The list is assembled in the `SourceRootList` MSBuild property inside `Rca.Runtime.csproj`. To include additional projects, update that property (semicolon-separated paths) — typically you want to include every project whose compiled assembly is merged into the runtime output.

- `source-hash.loader.txt` (configured in `src/Rca.Loader/Rca.Loader.csproj`):
  - `src/Rca.Loader`
  - `src/Rca.Loader.Contracts`

  Update the `SourceRootList` property in `Rca.Loader.csproj` to add/remove roots for loader hash computation.

---

## Runtime behavior (startup & detection)
1. On loader startup, `AssemblyStatusManager.InitializeOnStartup()` attempts to load `%LOCALAPPDATA%\RCA\LoadedAssemblies.json`.
2. If it doesn't exist, it computes initial values by:
   - Determining `loaderDir` (where the loader DLL is loaded from) and `runtimePath` (latest runtime deploy folder DLL path).
   - Reading `source-hash.loader.txt` (loader hash) and `source-hash.runtime.txt` (runtime hash) using the rules above.
   - If hashes are missing, it will try the runtime deploy root and finally attempt a developer fallback (compute hash from repo root).
3. When MSBuild signals a new deploy (hot-reload), `ProcessMsBuildSignal` reads the corresponding `source-hash.*.txt` in the temporary dll folder, compares with current values and updates `LoadedAssemblies.json` with `LastMSBuildSignal` and the event string (`only runtime outdated`, `only loader outdated`, etc.).

---

## Restart/validate flow
- `RestartManager.ValidateAssemblyCopy(source, target)` uses `source-hash.loader.txt` when available to confirm the copy succeeded. If the source or target hash file is missing, it falls back to a binary compare of the DLL files.
- The restart PowerShell script (external) is still used to perform the graceful restart/copy steps; its invocation is unchanged.

---

## Developer / CI suggestions
- CI should run `dotnet build` for `Rca.Runtime` and `Rca.Loader` (Release) to produce the timestamped deploy folders and hashes — tests or CI checks can fail if the expected `source-hash.runtime.txt` or `source-hash.loader.txt` are missing.
- If you want to ensure the runtime hash reflects every source change that contributes code to `Rca.Runtime`, keep the `SourceRootList` in `Rca.Runtime.csproj` in sync with all projects that are merged into the runtime binary.
- Consider embedding loader hash into the merged assembly during ILRepack if you want the addin to have a single authoritative source inside the DLL rather than relying on external files.

---

## How to test/verify locally
1. Build `Rca.Runtime` with deploy: `dotnet build src/Rca.Runtime -c Debug`. Confirm the new runtime folder and `source-hash.runtime.txt` exist in `%LOCALAPPDATA%\RCA\Runtime\<timestamp>`.
2. Build `Rca.Loader` with deploy: `dotnet build src/Rca.Loader -c Debug`. Confirm a `source-hash.loader.txt` appears in the latest runtime deploy folder (not copied to the addin folder).
3. Start Revit/Loader; check `%LOCALAPPDATA%\RCA\LoadedAssemblies.json` for recorded hashes and `LastMSBuildSignal`.
4. To run the generator manually:
   - `dotnet run --project src/Tools/SourceHashGenerator -- --roots "src/Rca.Runtime;src/Rca.Core;src/Rca.UI" --out "%LOCALAPPDATA%\\RCA\\Runtime\\manual-source-hash.txt"`

---

## Quick links (files you will likely inspect)
- Generator: `src/Tools/SourceHashGenerator/Program.cs`
- Runtime MSBuild integration: `src/Rca.Runtime/Rca.Runtime.csproj`
- Loader MSBuild integration: `src/Rca.Loader/Rca.Loader.csproj`
- AssemblyStatusManager: `src/Rca.Loader/AssemblyManagement/AssemblyStatusManager.cs`
- Restart/validation: `src/Rca.Loader/Restart/RestartManager.cs`
- Loader constants: `src/Rca.Loader/Infrastructure/LoaderConstants.cs`
