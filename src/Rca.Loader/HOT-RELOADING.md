# Hot-Reloading System (implementation-accurate)

This document describes how the hot-reload system actually works in this repository. It only contains information that is implemented in the codebase and points to the relevant files to make it easy for future maintainers or automated agents to find the implementation.

Important changes to be aware of
- There are two separate source-hash files produced by the build:
  - `source-hash.runtime.txt` — produced by the `Rca.Runtime` build and placed into the timestamped runtime deploy folder (under `%LOCALAPPDATA%\\RCA\\Runtime\\<timestamp>`).
  - `source-hash.loader.txt` — produced by the `Rca.Loader` build and placed into the same timestamped runtime deploy folder when the loader is deployed. It is NOT copied into the Revit addin folder to avoid accidental mismatch.
- The generator no longer writes any `source-hash.json` metadata; only a single-line hex SHA256 file is produced.

Quick file links (implementation)
- Source hash generator tool: `src/Tools/SourceHashGenerator/Program.cs`
- Runtime MSBuild integration (produces `source-hash.runtime.txt`): `src/Rca.Runtime/Rca.Runtime.csproj`
- Loader MSBuild integration (produces `source-hash.loader.txt`): `src/Rca.Loader/Rca.Loader.csproj`
- Loader constants (paths, filenames): `src/Rca.Loader/Infrastructure/LoaderConstants.cs`
- Assembly status tracking: `src/Rca.Loader/AssemblyManagement/AssemblyStatusManager.cs`
- Restart helper and validation: `src/Rca.Loader/Restart/RestartManager.cs`
- Runtime manager (load/unload runtime in collectible context): `src/Rca.Loader/Services/RuntimeManager.cs`
- Custom load context (resolving and non-collectible assemblies): `src/Rca.Loader/Infrastructure/RuntimeLoadContext.cs`
- Pipe server (receives RELOAD/RELOAD_RUNTIME/etc): `src/Rca.Loader/Services/PipeServerService.cs`
- Runtime command handling (payload processing): `src/Rca.Loader/Infrastructure/RuntimeCommandHandler.cs`
- Reload external command (UI entry): `src/Rca.Loader/Commands/ReloadRuntimeCommand.cs`

## LoadedAssemblies.json — when and how it is updated

Path and format
- The JSON file path is defined in `LoaderConstants.LoadedAssembliesJsonPath` (`src/Rca.Loader/Infrastructure/LoaderConstants.cs`).
- The file stores a `LoadedAssembliesInfo` object (see `src/Rca.Loader/AssemblyManagement/AssemblyStatusManager.cs` usage). It contains at minimum:
  - `LoaderComponents.Path` and `LoaderComponents.Hash`
  - `RuntimeAssembly.Path` and `RuntimeAssembly.Hash`
  - `LastMSBuildSignal.Time` and `LastMSBuildSignal.Event`

Primary update operations (exact code locations)
- `AssemblyStatusManager.SaveAssemblyInfo(LoadedAssembliesInfo info)`
  - Serializes the `LoadedAssembliesInfo` to JSON and writes it to `LoadedAssembliesJsonPath`.
  - Ensures target directory exists before writing.
  - Location: `src/Rca.Loader/AssemblyManagement/AssemblyStatusManager.cs`.

When Save is called (conditions and callers)
1. Initial creation on startup
   - Method: `AssemblyStatusManager.InitializeOnStartup()`
   - Behavior: If the JSON file is missing or cannot be loaded, the manager computes initial paths and hashes (reading `source-hash.*.txt` files or computing fallbacks) and calls `SaveAssemblyInfo` to persist initial state.
   - Location: `AssemblyStatusManager.InitializeOnStartup()` in `src/Rca.Loader/AssemblyManagement/AssemblyStatusManager.cs`.

2. When an MSBuild / deploy signal is processed
   - Method: `AssemblyStatusManager.ProcessMsBuildSignal(string tempDllPath)`
   - Behavior: Reads `source-hash.loader.txt` and `source-hash.runtime.txt` from the provided deploy folder, determines event type (`only loader outdated`, `only runtime outdated`, `both`, or `no changes`) and calls `UpdateSignalInfo(...)`.
   - `UpdateSignalInfo(string eventType)` writes the `LastMSBuildSignal` fields and calls `SaveAssemblyInfo`.
   - Location: `src/Rca.Loader/AssemblyManagement/AssemblyStatusManager.cs`.

3. After a successful runtime reload
   - Method: `AssemblyStatusManager.UpdateHashesAfterReload(string runtimePath)`
   - Behavior: Called after `RuntimeManager.ReloadRuntime(...)` returns success (e.g. from `RuntimeCommandHandler` or `ReloadRuntimeCommand`). This updates `RuntimeAssembly.Path` and `RuntimeAssembly.Hash` (reading `source-hash.runtime.txt` from the runtime folder or runtime root) and then calls `SaveAssemblyInfo`.
   - Locations involved: `src/Rca.Loader/Services/RuntimeManager.cs` (reload) and `AssemblyStatusManager.UpdateHashesAfterReload`.

4. After a successful loader restart/copy
   - Method: `AssemblyStatusManager.UpdateLoaderComponentsHashesAfterRestart(string loaderDir)`
   - Behavior: Called after the loader has been replaced in the addin folder and validated (e.g. via `RestartManager.ValidateAssemblyCopy`). It updates `LoaderComponents.Path` and `LoaderComponents.Hash` and calls `SaveAssemblyInfo`.
   - Location: `src/Rca.Loader/AssemblyManagement/AssemblyStatusManager.cs` and `src/Rca.Loader/Restart/RestartManager.cs`.

5. When signal info is updated explicitly
   - Method: `AssemblyStatusManager.UpdateSignalInfo(string eventType)`
   - Behavior: Updates `LastMSBuildSignal` with current time and event type and saves JSON.
   - Used by: `ProcessMsBuildSignal` and other signal-related flows.

Other behaviors and notes
- Directory creation: `EnsureDirectoriesExist()` in `AssemblyStatusManager` will create the runtime deploy root and the JSON directory as needed prior to any save operations.
- Read/write resilience: Load/Save methods catch exceptions and log them; writes are best-effort and won't crash Revit if they fail.
- File contents changed by these operations:
  - `LoadedAssemblies.json` is overwritten whenever `SaveAssemblyInfo` is invoked.
  - `source-hash.runtime.txt` and `source-hash.loader.txt` are not written by this component — they are generated by MSBuild targets in `Rca.Runtime.csproj` and `Rca.Loader.csproj` respectively; `AssemblyStatusManager` only reads them.
- Developer reset: Deleting `LoadedAssemblies.json` forces `InitializeOnStartup()` to recompute and re-create the JSON on next startup.

## Overview of the pieces

1) Source hash generation
- Tool: `src/Tools/SourceHashGenerator/Program.cs`
  - Accepts `--root` or `--roots` and writes a single-line SHA256 hex string to `--out`.
  - Normalizes text line endings to LF and sorts files deterministically.
  - Default scanned extensions include `.cs`, `.xaml`, `.csproj`, `.props`, etc.
  - It ignores `bin`, `obj`, `.git`, `.vs`, `node_modules`, `packages` directories.

- How MSBuild uses the tool:
  - `Rca.Runtime` (`src/Rca.Runtime/Rca.Runtime.csproj`) runs the generator with `--roots` set to the projects that are merged into `Rca.Runtime` (for example: `src\\Rca.Runtime;src\\Rca.Core;src\\Rca.UI;src\\Rca.Network;src\\Rca.Contracts`) and writes `source-hash.runtime.txt` into the runtime deploy folder.
  - `Rca.Loader` (`src/Rca.Loader/Rca.Loader.csproj`) runs the generator with `--roots` set to `src\\Rca.Loader;src\\Rca.Loader.Contracts` and writes `source-hash.loader.txt` into the runtime deploy folder for that build.

Notes:
- `source-hash.runtime.txt` reflects the combined source state of all projects that are compiled/merged into the runtime bundle.
- `source-hash.loader.txt` reflects the loader source (loader + loader contracts).
- The build tasks that produce the hashes are in the `<Target>` elements named `DeployRuntime` and `DeployLoaderToTemp` inside the respective `.csproj` files.

2) Where hashes live at runtime
- Runtime deploy root: `LoaderConstants.RuntimeDeployRoot` — defined in `src/Rca.Loader/Infrastructure/LoaderConstants.cs` (typically `%LOCALAPPDATA%\RCA\Runtime`).
- Each build deploy uses a timestamped folder under that root, and `source-hash.*.txt` files are placed in that folder.
- The loader no longer copies `source-hash.loader.txt` into the Revit addin folder to avoid mismatches.

3) Detection and state tracking (Loader side)
- `AssemblyStatusManager` (`src/Rca.Loader/AssemblyManagement/AssemblyStatusManager.cs`) is responsible for:
  - Loading/saving the persisted state at `%LOCALAPPDATA%\RCA\LoadedAssemblies.json` (path available in `LoaderConstants.LoadedAssembliesJsonPath`).
  - On startup it attempts to populate `LoaderComponents` and `RuntimeAssembly` fields by reading:
    - `source-hash.loader.txt` (loader hash) from the loader's directory first, then from the latest runtime deploy folder or runtime root if necessary.
    - `source-hash.runtime.txt` (runtime hash) from the latest runtime deploy folder or the runtime root.
  - Exposes APIs: `IsLoaderOutdated()`, `IsRuntimeOutdated()`, `ProcessMsBuildSignal(...)`, `UpdateHashesAfterReload(...)`, `UpdateLoaderComponentsHashesAfterRestart(...)`.

- It does not assume loader hash in the addin folder; it searches runtime deploy folders and falls back to computing a hash from a repo root when appropriate.

4) Hot-reload command flow (pipe & UI)
- `PipeServerService` (`src/Rca.Loader/Services/PipeServerService.cs`) listens on `LoaderConstants.PipeName` and forwards decoded `PipeCommand` objects to a handler.
- `RuntimeCommandHandler` (`src/Rca.Loader/Infrastructure/RuntimeCommandHandler.cs`) handles commands including `RELOAD_RUNTIME`:
  - For `RELOAD_RUNTIME` it calls `AssemblyStatusManager.ProcessMsBuildSignal(tempDllPath)` to update status from the deploy folder.
  - It decides whether a restart is required if only loader is outdated (returns `LOADER_RESTART_REQUIRED`), or proceeds to call `RuntimeManager.ReloadRuntime(...)` when runtime reload is needed.
  - When runtime reload succeeds it calls `AssemblyStatusManager.UpdateHashesAfterReload(...)`.

- There is also a manual Revit UI entrypoint `ReloadRuntimeCommand` (`src/Rca.Loader/Commands/ReloadRuntimeCommand.cs`) used by the user to trigger reload/restart flows from the ribbon.

5) Runtime loading/unloading
- `RuntimeManager` (`src/Rca.Loader/Services/RuntimeManager.cs`) loads the runtime into a collectible `RuntimeLoadContext`.
  - `RuntimeLoadContext` (`src/Rca.Loader/Infrastructure/RuntimeLoadContext.cs`) provides custom resolving logic for assemblies and ensures certain assemblies (e.g. `IronPython` set) are loaded in the default context to avoid collectible issues.
  - `RuntimeManager.ReloadRuntime(folder, out error)` is used both from the command handler and UI flow.
  - `RuntimeManager.ReloadLatest(out error)` locates the latest timestamped folder under `LoaderConstants.RuntimeDeployRoot` and calls `ReloadRuntime`.

6) Restart and validation
- `RestartManager` (`src/Rca.Loader/Restart/RestartManager.cs`) shows the restart dialog and executes an external PowerShell script to copy files and restart Revit when loader components are updated.
  - `RestartManager.ValidateAssemblyCopy(sourcePath, targetPath)` compares `source-hash.loader.txt` files when present (fallback to binary compare) to validate the copy.

7) What is not done / design decisions reflected in code
- The loader hash is NOT copied into the Revit addin folder by the build — this avoids potential mismatch issues. (See `src/Rca.Loader/Rca.Loader.csproj`.)
- There is no `source-hash.json` metadata written by the generator; only the raw hex string file is produced by `SourceHashGenerator`.
- The system chooses to produce separate loader/runtime hashes rather than a single solution-level hash. This is implemented in MSBuild targets.

How to reproduce common scenarios locally
- Build runtime (writes `source-hash.runtime.txt` into a timestamped folder):
  - `dotnet build src/Rca.Runtime -c Debug`
  - Inspect `%LOCALAPPDATA%\RCA\Runtime\<latest>` for `source-hash.runtime.txt`.

- Build loader (writes `source-hash.loader.txt` into the timestamped folder, but does not copy it into the addin folder):
  - `dotnet build src/Rca.Loader -c Debug`
  - Inspect `%LOCALAPPDATA%\RCA\Runtime\<latest>` for `source-hash.loader.txt`.

- Trigger a reload via named pipe (example from MSBuild step): the `RELOAD_RUNTIME` command payload is the path to the timestamped runtime folder. The `RuntimeCommandHandler` will process it and decide whether to reload runtime or request a loader restart.

Files to inspect for troubleshooting
- `src/Tools/SourceHashGenerator/Program.cs` — generator implementation and CLI options.
- `src/Rca.Runtime/Rca.Runtime.csproj` — see `DeployRuntime` MSBuild target.
- `src/Rca.Loader/Rca.Loader.csproj` — see `DeployLoaderToTemp` MSBuild target.
- `src/Rca.Loader/AssemblyManagement/AssemblyStatusManager.cs` — startup/load/save logic and hash lookups.
- `src/Rca.Loader/Infrastructure/RuntimeCommandHandler.cs` — pipe command handling and reload logic.
- `src/Rca.Loader/Services/RuntimeManager.cs` and `src/Rca.Loader/Infrastructure/RuntimeLoadContext.cs` — runtime loading and unloading.
- `src/Rca.Loader/Restart/RestartManager.cs` — restart dialog and copy validation.

Notes for maintainers / automated agents
- Keep `SourceRootList` in the `.csproj` targets in sync with what is actually merged into the runtime or loader. If you add a project to the ILRepack inputs, add it to the `--roots` list for the runtime hash.
- If you want the addin to carry an authoritative loader hash, consider embedding the loader hash into the merged `Rca.Loader.dll` as an assembly attribute or resource at merge-time. That is *not* implemented in the current codebase.
- Unit tests should assume the generator is deterministic given the same roots and an unchanged working tree.

Contact points in code (quick)
- Generator: `src/Tools/SourceHashGenerator/Program.cs`
- Runtime deploy: `src/Rca.Runtime/Rca.Runtime.csproj` (target `DeployRuntime`)
- Loader deploy: `src/Rca.Loader/Rca.Loader.csproj` (target `DeployLoaderToTemp`)
- Assembly state: `src/Rca.Loader/AssemblyManagement/AssemblyStatusManager.cs`
- Command handling: `src/Rca.Loader/Infrastructure/RuntimeCommandHandler.cs`
- Pipe server: `src/Rca.Loader/Services/PipeServerService.cs`
- Runtime load: `src/Rca.Loader/Services/RuntimeManager.cs` and `src/Rca.Loader/Infrastructure/RuntimeLoadContext.cs`

This document intentionally avoids speculation and focuses on the current code. If you change the build or deployment flow, update this document accordingly.

---

## Named-pipe protocol used by the hot-reload system

This section documents the exact messages, payloads and responses used by the hot-reload system over the named pipe. Implementation references are provided so an agent or developer can locate parsing/validation and handling code.

Files that implement the protocol
- Server and message wire format: `src/Rca.Loader/Services/PipeServerService.cs` (listening loop, JSON per-line, one command per connection).
- Command names and validation: `src/Rca.Loader/Infrastructure/CommandValidationService.cs` (contains `PipeCommands` constants and validation rules).
- Command handling and semantics: `src/Rca.Loader/Infrastructure/RuntimeCommandHandler.cs` (handler methods for each supported command).
- Standardized responses: `src/Rca.Loader/Infrastructure/PipeResponseFactory.cs` (status constants and helper creators).

Wire format (JSON)
- Client -> Server: send a single JSON object encoded as UTF-8, terminated by newline. Server reads one line and deserializes into `PipeCommand`.
  - Shape (server expects):
    {
      "Command": "RELOAD_RUNTIME",
      "Payload": "C:\\\\Users\\\\...\\\\Rca.Runtime\\\\20251012-123456"
    }
  - `Command` is case-insensitive in the handler switch, but validation uses uppercase constants (see `PipeCommands`).
  - `Payload` is optional depending on `Command` (see validation rules below).

- Server -> Client: server writes a single JSON response object (one line) with the shape:
    {
      "Status": "OK" | "ERROR" | "LOADED" | "EMPTY",
      "Message": "optional human-readable message or data"
    }
  - `Status` values are defined in `PipeResponseStatus` inside `PipeResponseFactory.cs`.
  - `Message` semantics vary by command (see responses below).

Transport details
- The server accepts one command per connection and sends one response per connection. It uses a 64KB input/output buffer size by default (`PipeServerService`), and gracefully handles connection drops.
- The server reads the first line from the stream and attempts JSON deserialization; malformed JSON leads to an error response.

Commands relevant to hot-reload (non-test)
- `RELOAD` (constant: `PipeCommands.Reload`)
  - Purpose: Ask the loader to reload runtime from the specified folder path.
  - Payload: required string — path to the folder that contains runtime DLLs.
  - Validation: `Payload` must be a non-empty folder path (see `CommandValidationService.ValidateReloadCommand`).
  - Handler: `RuntimeCommandHandler.HandleReloadCommand(PipeCommand)`
    - Calls `RuntimeManager.ReloadRuntime(payload, out error)`.
    - On success, calls `AssemblyStatusManager.ProcessMsBuildSignal(payload)` to register the deploy signal.
  - Responses:
    - Success: { "Status": "OK", "Message": "<optional message>" }
    - Failure: { "Status": "ERROR", "Message": "<error message>" }

- `RELOAD_RUNTIME` (constant: `PipeCommands.ReloadRuntime`)
  - Purpose: High-level CI/CD-friendly command used by MSBuild/deploy step to notify the running loader about a new timestamped deploy folder and request an appropriate action (reload runtime or indicate a loader restart is required).
  - Payload: required string — path to the timestamped runtime deploy folder produced by build (the folder containing `Rca.Runtime.dll` and source-hash files).
  - Validation: `Payload` must be a non-empty folder path (see `CommandValidationService.ValidateReloadRuntimeCommand`).
  - Handler: `RuntimeCommandHandler.HandleReloadRuntimeCommand(PipeCommand)`
    - Steps performed by handler:
      1. Calls `AssemblyStatusManager.ProcessMsBuildSignal(payload)` which reads `source-hash.loader.txt` and `source-hash.runtime.txt` in that folder and updates `LastMSBuildSignal` (via `UpdateSignalInfo`).
      2. Calls `AssemblyStatusManager.IsLoaderOutdated()` and `IsRuntimeOutdated()` to determine what changed.
      3. If only loader is outdated (loader changed but runtime did not), it returns success with message `LOADER_RESTART_REQUIRED` to indicate that the Loader must be replaced and Revit restarted.
      4. If neither is outdated, it returns success with message `NO_ACTION_NEEDED`.
      5. Otherwise (runtime changed or both changed) it attempts `RuntimeManager.ReloadRuntime(payload, out error)` to load the new runtime from that folder. On success it calls `AssemblyStatusManager.UpdateHashesAfterReload(...)` and returns success with message `ReloadRuntime completed successfully`.
    - Responses (examples):
      - Loader restart required: { "Status": "OK", "Message": "LOADER_RESTART_REQUIRED" }
      - No action needed: { "Status": "OK", "Message": "NO_ACTION_NEEDED" }
      - Runtime reload success: { "Status": "OK", "Message": "ReloadRuntime completed successfully" }
      - Reload failed: { "Status": "ERROR", "Message": "<error details>" }

- `STATUS` (constant: `PipeCommands.Status`)
  - Purpose: Query whether a runtime is currently loaded and, if so, which path.
  - Payload: not required (ignored if present).
  - Handler: `RuntimeCommandHandler.HandleStatusCommand()`
    - Checks `RuntimeManager.IsRuntimeLoaded` and `RuntimeManager.CurrentRuntimePath`.
    - Responses:
      - If runtime loaded: { "Status": "LOADED", "Message": "<path to loaded runtime DLL>" }
      - If not loaded: { "Status": "EMPTY", "Message": "" }
      - On error: { "Status": "ERROR", "Message": "<error details>" }

Validation rules summary
- Command names are validated against the known set in `PipeCommands`.
- `RELOAD` and `RELOAD_RUNTIME` require a non-empty `Payload` path.
- `STATUS` and `TEST_INIT` accept empty payloads.
- Invalid payloads receive an `InvalidPayload` response created by `PipeResponseFactory.InvalidPayload(...)`.


Behavioral notes (observed in code)
- The server treats each incoming connection as a single request/response exchange and then closes the connection.
- The handler logs and returns high-level strings (e.g. `LOADER_RESTART_REQUIRED`) for orchestrator use; CI scripts should inspect `Message` to decide whether to trigger a restart sequence.
- `RELOAD_RUNTIME` is the recommended entrypoint for automated builds/CI — MSBuild/deploy should send the path to the timestamped deploy folder as the `Payload`.
- The pipe protocol is intentionally simple: small JSON messages, human-readable `Message` field for easy debugging, and a fixed set of `Status` tokens for programmatic checks.

Examples
- Request (JSON line):
  { "Command": "RELOAD_RUNTIME", "Payload": "C:\\Users\\dev\\AppData\\Local\\RCA\\Runtime\\20251012-123456" }

- Response (runtime reload success):
  { "Status": "OK", "Message": "ReloadRuntime completed successfully" }

- Response (loader restart required):
  { "Status": "OK", "Message": "LOADER_RESTART_REQUIRED" }

- Status request and response:
  Request: { "Command": "STATUS", "Payload": null }
  Response: { "Status": "LOADED", "Message": "C:\\Users\\dev\\AppData\\Local\\RCA\\Runtime\\20251012-123456\\Rca.Runtime.dll" }

Where to change/extend
- Add or change commands in `CommandValidationService` (`src/Rca.Loader/Infrastructure/CommandValidationService.cs`) and `PipeCommands` constant list.
- Implement handling logic in `RuntimeCommandHandler` (`src/Rca.Loader/Infrastructure/RuntimeCommandHandler.cs`).
- Adjust wire-level behavior (buffer sizes, one-command-per-connection) in `PipeServerService` (`src/Rca.Loader/Services/PipeServerService.cs`).
