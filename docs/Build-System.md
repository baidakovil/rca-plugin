# Build System – Why It Works This Way

## Challenges with implementing a build system for a hot‑reloading workflow
- Autodesk Revit keeps loaded DLLs locked. Overwriting the same deployment folder during rebuilds leads to MSB3026 copy failures. That is the reason for creating hot-reloading system, where DLLs of each build are copied into fresh folder with timestamped name.
- Solution builds run in parallel across multiple projects and MSBuild nodes/processes. All projects participating in a single build need a shared build identifier to deploy into the same, fresh target folder.
- Visual Studio triggers design‑time (evaluation) builds that must not affect deployment or hot‑reload state.

## Core decisions
1) Per‑build deployment folder by timestamp
- Each real build emits artifacts into a fresh `%LOCALAPPDATA%\RCA\Runtime\yyyyMMdd_HHmmss` subfolder.
- Rationale: prevent overwriting DLLs currently loaded by Revit and enable switching to a new version by folder.

2) Single timestamp per solution build
- All projects reuse the same timestamp so Loader and Runtime deploy to the same destination.
- Rationale: consistency across project groups; avoid producing multiple folders for a single build.

3) Cross‑process coordination via a global mutex + text file
- A named OS mutex (`Global\RCA_BuildStamp`) and a shared file `build\artifacts\hashes\timestamp.txt` synchronize parallel builds.
- The first project in a real build (non‑design‑time) writes a new local‑time timestamp; all other projects only read it.
- Why a mutex: parallel MSBuild nodes can start concurrently; without a mutex, write races produce different timestamps.
- Why a file: the timestamp must be shared across processes; a small file is a simple and robust IPC medium for MSBuild.

4) Ignore design‑time builds entirely
- All targets that influence timestamp or deployment execute only when `DesignTimeBuild != true`.
- Rationale: design‑time evaluation builds should not mutate deployment state.

## Build pipeline overview
- `EnsureRcaTimestamp` – ensures a single timestamp for the current build (under mutex, writes/reads `timestamp.txt`).
- `GenerateHash` – computes source hashes for Loader/Runtime groups (diagnostics and version traceability).
- `EmitAssemblyMetadataSource` – generates `[assembly: AssemblyMetadata("DeployFolder", "<timestamp>")]` and `SourceHash` into a temporary compile unit.
- `DeployLoaderGroup` / `DeployRuntimeGroup` – copy the corresponding DLLs into `%LOCALAPPDATA%\RCA\Runtime\<timestamp>`.
- `NotifyBuildCompleted` – optionally sends a pipe signal so the Loader can detect a new runtime drop.

## Risks and failure modes avoided
- Multiple deployment folders per single build: a shared timestamp eliminates cross‑project drift.
- Copying over locked DLLs: every real build uses a new folder; the previous one can remain open in Revit safely.
- Design‑time induced races: excluded from the pipeline.

## Operational guidance
- A new build creates a new folder automatically. If it appears to deploy into an old one, inspect `build\artifacts\hashes\timestamp.txt` and look for `EnsureRcaTimestamp` messages in the MSBuild log.
- Manual reset is rarely needed. If required, delete `build\artifacts\hashes\timestamp.txt` before a build.
- If Revit still holds an older folder open, that is expected. The next build lands in a fresh folder; the Loader can reload the newest drop.

## Why not environment variables or pure MSBuild properties
- Environment variables are brittle in local/IDE workflows and easy to forget or override.
- A pure MSBuild property is evaluated per project/node without inter‑process synchronization, which causes races.
- Mutex + file provide a minimal, deterministic, cross‑process coordination mechanism.

## Artifacts and locations
- Timestamp file: `build\artifacts\hashes\timestamp.txt`
- Timestamp format: local time `yyyyMMdd_HHmmss` (e.g., `20251018_112825`)
- Deployment root: `%LOCALAPPDATA%\RCA\Runtime\<timestamp>`
