# Build System – Why It Works This Way

## Challenges with implementing a build system for a hot‑reloading workflow
- Autodesk Revit keeps loaded DLLs locked. Overwriting the same deployment folder during rebuilds leads to MSB3026 copy failures.
- Solution builds run in parallel across multiple projects and MSBuild nodes/processes. All projects participating in a single build need a shared build identifier to deploy into the same, fresh target folder.
- Visual Studio triggers design‑time (evaluation) builds that must not affect deployment or hot‑reload state.
- Test runners often perform multiple independent MSBuild invocations (discovery/build/run). Those invocations must still share one deployment folder.

## Core decisions
1) Per‑build deployment folder by timestamp
- Each real build emits artifacts into a fresh `%APPDATA%\Autodesk\Revit\Addins\$(RcaRevitVersion)\yyyyMMdd_HHmmss` subfolder.
- Rationale: prevent overwriting DLLs currently loaded by Revit and enable switching to a new version by folder. Deploying under the Revit Addins path makes it straightforward for Revit to locate the addin and for the `.addin` manifest to reference a relative assembly path.

2) Single timestamp per solution build
- All projects reuse the same timestamp so Loader and Runtime deploy to the same destination.
- Rationale: consistency across project groups; avoid producing multiple folders for a single build.

3) Cross‑process coordination via a global mutex + text file
- A named OS mutex (`Global\RCA_BuildStamp`) and a shared file `%APPDATA%\Autodesk\Revit\Addins\$(RcaRevitVersion)\Timestamp.txt` synchronize parallel builds.
- The first project in a real build (non‑design‑time) writes a new local‑time timestamp; all other projects only read it.
- Why a mutex: parallel MSBuild nodes can start concurrently; without a mutex, write races produce different timestamps.
- Why a file: the timestamp must be shared across processes; a small file is a simple and robust IPC medium for MSBuild.

4) Sticky session TTL + manual override
- Timestamp reuse is sticky for a configurable TTL: `RcaStickyStampSeconds` (default 30). If the timestamp file is younger than TTL, the same folder is reused across separate MSBuild invocations (e.g., test discovery/build/run).
- Manual override: set `RcaForceNewStamp=true` (or `1`) to force a fresh timestamp for the next build, ignoring TTL.
- Rationale: test runners and IDEs often spawn multiple builds; TTL prevents accidental drift while keeping control when a new folder is required.

5) Make the logic robust and maintainable
- The timestamp logic is implemented as a dedicated PowerShell script stored in `build\Scripts\EnsureRcaStamp.ps1` and invoked with `-File`. This avoids fragile inline quoting/escaping and improves readability.
- All timestamp‑affecting targets run only for real builds: `DesignTimeBuild != true`.

## Build pipeline overview
- `EnsureRcaTimestamp` (`build/targets/timestamp-management.targets`) – runs `build\Scripts\EnsureRcaStamp.ps1` to create/reuse a timestamp under a global mutex, with TTL and force override; exports `RcaHotReloadTimestamp` MSBuild property.
- `ComputeSourceHashes` (`build/targets/hash-generation.targets`) – computes source hashes for Loader/Runtime groups by invoking `Rca.SourceHashGenerator` tool, which creates marker files (`SourceHash-Loader-<hash>.txt`, `SourceHash-Runtime-<hash>.txt`) in the deploy timestamp folder.
- `AddHashMarkersToAdditionalFiles` (`build/targets/hash-generation.targets`) – adds marker files to `AdditionalFiles` so Source Generator can read them.
- Source Generator (`src/Tools/Rca.BuildMetadata.Generator`) – reads marker files from `AdditionalFiles`, extracts hashes, and generates `Rca.AssemblyMetadata.g.cs` with `[assembly: AssemblyMetadata("SourceHash", "<hash>")]` and `[assembly: AssemblyMetadata("DeployFolder", "<timestamp>")]`. Also emits `Rca.Generated.RcaBuildMetadata` class in `Rca.Contracts` to expose build-time constants.
- `DeployLoaderGroup` / `DeployRuntimeGroup` (`build/targets/deployment.targets`) – copy the corresponding DLLs into the timestamped folder: `%APPDATA%\Autodesk\Revit\Addins\$(RcaRevitVersion)\<timestamp>`.
- `NotifyBuildCompleted` (`build/targets/build-notification.targets`) – optionally sends a pipe signal so the Loader can detect a new runtime drop.
- `GenerateRcaAddinFile` (`build/targets/timestamp-management.targets`) – runs after `EnsureRcaTimestamp`, reads the template at `build/Resources/Rca.addin.template` and substitutes the `RcaAddinAssemblyRelativePath` token with the computed deploy folder path.

## Notes on configuration
- Template path: `RcaAddinTemplatePath` (default `$(SolutionDir)build\Resources\Rca.addin.template`) is configured in `build/paths.props`.
- Revit references are delivered via `build/references-revit.props` and applied opt‑in by projects that set `<IncludeRevitReferences>true</IncludeRevitReferences>`; this prevents tool and generator projects from pulling Revit assemblies unintentionally.

## Risks and failure modes avoided
- Multiple deployment folders per single build: a shared timestamp eliminates cross‑project drift.
- Copying over locked DLLs: every real build uses a new folder; the previous one can remain open in Revit safely.
- Design‑time induced races: excluded from the pipeline.
- Test‑run drift across invocations: sticky TTL keeps a single folder across discovery/build/run phases.

## Operational guidance
- New folder per real build is automatic. If it appears to deploy into an old one, inspect the timestamp file at `%APPDATA%\Autodesk\Revit\Addins\$(RcaRevitVersion)\Timestamp.txt` and look for `EnsureRcaTimestamp` messages in the MSBuild log.
- Change the sticky window: `/p:RcaStickyStampSeconds=90`.
- Force a fresh folder: `/p:RcaForceNewStamp=true` (or delete the timestamp file).
- If Revit still holds an older folder open, that is expected. The next build lands in a fresh folder; the Loader can reload the newest drop.

## Why not environment variables or pure MSBuild properties
- Environment variables are brittle in local/IDE workflows and easy to forget or override.
- A pure MSBuild property is evaluated per project/node without inter‑process synchronization, which causes races.
- Mutex + file provide a minimal, deterministic, cross‑process coordination mechanism.

## Artifacts and locations
- Timestamp file: `%APPDATA%\Autodesk\Revit\Addins\$(RcaRevitVersion)\Timestamp.txt` (created by `EnsureRcaStamp.ps1` under global mutex)
- Timestamp format: local time `yyyyMMdd_HHmmss` (e.g., `20251018_112825`)
- Deployment root: `%APPDATA%\Autodesk\Revit\Addins\$(RcaRevitVersion)\<timestamp>`
- Marker files: `SourceHash-Loader-<hash>.txt` and `SourceHash-Runtime-<hash>.txt` in the deploy timestamp folder
