# Hot-Reload System (HRS)

This document describes the Revit Chat Assistant (RCA) Hot-Reload System architecture, implementation, and operational behavior.

---

## Objectives

- **Hot-reload Runtime** without restarting Revit, preserving UI state and user context.
- **Rebuild/Reload Loader** safely via Revit restart orchestrated by the HRS.
- **Track versions** of Loader and Runtime assemblies using source-code hashes embedded at build time.
- **Notify** users interactively when new builds are available, giving them control over when to apply updates.

---

## Architecture overview

The HRS consists of:

1. **Build-time tools** (SourceHashGenerator, AttributeInjector) that embed version metadata into assemblies.
2. **MSBuild integration** that deploys to timestamped folders and sends notification signals.
3. **Named Pipe protocol** for MSBuild-to-Revit communication.
4. **AssemblyStatusManager** that tracks loaded versions and detects updates.
5. **Interactive user dialogs** that prompt for reload/restart actions.
6. **RuntimeManager** that performs collectible assembly loading for hot-reload.

---

## Build signal flow (simplified architecture)

### New simplified flow (current implementation)

```
MSBuild Build                    Revit Addin
    |                                |
    |-- Deploy DLLs to folder -->    |
    |                                |
    |-- Send BUILD_COMPLETED -->     |
    |                                |
                                     |-- Find latest folder
                                     |-- Read hashes from DLLs
                                     |-- Compare with loaded
                                     |-- Show user dialog
                                     |     - Reload Runtime?
                                     |     - Restart Revit?
                                     |     - Ignore?
```

**Benefits:**
- ✅ No complex payload in pipe message
- ✅ No path escaping or JSON complexity
- ✅ Single source of truth (addin finds latest folder)
- ✅ Better UX (interactive dialog, user controls timing)
- ✅ More reliable (simpler protocol)

---

## Named Pipe protocol

### Connection

- **Pipe name:** `RCA_PIPE`
- **Direction:** InOut (bidirectional)
- **Format:** JSON lines (one command per line, one response per line)
- **Lifecycle:** Each connection handles exactly one command, then disconnects

### Commands

All commands are JSON objects with `Command` and `Payload` fields:

```json
{
  "Command": "BUILD_COMPLETED",
  "Payload": ""
}
```

#### BUILD_COMPLETED

**Purpose:** Notify addin that MSBuild has deployed a new build.

**Payload:** Empty string (optional and ignored)

**Behavior:**
1. Addin finds latest folder in `%LOCALAPPDATA%\RCA\Runtime`
2. Reads `SourceHash` metadata from `Rca.Loader.dll` and `Rca.Runtime.dll`
3. Compares with currently loaded assemblies
4. Shows interactive dialog to user via ExternalEvent
5. User chooses: Reload Runtime, Restart Revit, or Ignore

**Response:**
```json
{
  "Status": "OK",
  "Message": "User prompted for reload action"
}
```

#### RELOAD_RUNTIME

**Purpose:** Reload runtime assembly (legacy command, kept for backward compatibility)

**Payload:** Optional folder path (if empty, finds latest automatically)

**Behavior:** Same as BUILD_COMPLETED but without user dialog - performs automatic reload

#### STATUS

**Purpose:** Query current runtime status

**Payload:** Empty

**Response:**
```json
{
  "Status": "LOADED",
  "Message": "C:\\Users\\...\\Runtime\\20250115-143022\\Rca.Runtime.dll"
}
```

#### RUN_TESTS

**Purpose:** Execute tests in Revit context (used by test adapter)

**Payload:** JSON test execution payload

#### TEST_INIT

**Purpose:** Initialize test execution environment

**Payload:** Optional

### Validation rules summary

- Command names are validated against the known set in `PipeCommands`.
- `BUILD_COMPLETED`, `STATUS`, `TEST_INIT` accept empty payloads.
- `RELOAD`, `RELOAD_RUNTIME` accept optional payloads.
- `RUN_TESTS` requires valid test payload.
- Invalid payloads receive an `InvalidPayload` response.

### Behavioral notes

- The server treats each incoming connection as a single request/response exchange and then closes the connection.
- MSBuild should send `BUILD_COMPLETED` after successful deployment.
- The addin decides autonomously what changed by reading hashes from disk.
- User gets full control via interactive dialog - no automatic interruptions.

### Examples

**Build notification request:**
```json
{ "Command": "BUILD_COMPLETED", "Payload": "" }
```

**Response:**
```json
{ "Status": "OK", "Message": "User prompted for reload action" }
```

**Status request:**
```json
{ "Command": "STATUS", "Payload": null }
```

**Response:**
```json
{ "Status": "LOADED", "Message": "C:\\Users\\dev\\AppData\\Local\\RCA\\Runtime\\20251012-123456\\Rca.Runtime.dll" }
```

### Where to change/extend

- Add or change commands in `CommandValidationService` and `PipeCommands` constant list.
- Implement handling logic in `RuntimeCommandHandler`.
- Adjust wire-level behavior in `PipeServerService`.
- Update interactive dialog in `ShowReloadDialogHandler`.

---

## Dockable Panel UI Hot-Reload

The system supports hot-reloading of the dockable panel UI without restarting Revit. This is achieved through a proxy pattern that separates the persistent Loader context from the collectible Runtime context.

Key components:
- `SharedServiceRegistry` - Static cross-context service registry
- `DockablePanelHost` - Persistent placeholder in Loader
- `RuntimePanelFactory` - Factory registered by Runtime
- `IRuntimePanelFactory` - Contract interface for cross-context UI creation
- `IRuntimePanelHost` - Contract interface for content injection

UI reload flow:
1. Loader registers `DockablePanelHost` with Revit on startup
2. Runtime loads and registers `RuntimePanelFactory` in `SharedServiceRegistry`
3. `RuntimeManager.CreateRuntimeDockableContent()` resolves factory
4. Factory creates `RcaDockablePanel` with resolved dependencies
5. UI is injected into `DockablePanelHost` via `SetContent()`
6. Pane is shown after successful content injection
7. On Runtime unload, panel content is cleared

See `docs/HRS-UI-Hotreload-Architecture.md` for detailed architecture documentation.

---

## User Experience Flow

### Runtime-only update

When MSBuild notifies about a build and only Runtime changed:

1. **Dialog appears:**
   ```
   New Build Available
   
   New version detected
   • Runtime assembly updated
   
   [Reload Runtime Now]
     Apply changes without restarting Revit
   
   [Ignore for Now]
     Continue with current version
   ```

2. **User chooses action:**
   - Reload → Runtime reloads, UI updates, work continues
   - Ignore → Dialog closes, can reload later via button

### Loader + Runtime update

When Loader components also changed:

1. **Dialog appears:**
   ```
   New Build Available
   
   New version detected
   • Loader components updated
   • Runtime assembly updated
   
   [Restart Revit Now]
     Required to apply Loader updates
   
   [Just Reload Runtime]
     Partial update - Loader changes will be ignored
   
   [Ignore for Now]
     Continue with current version
   ```

2. **User chooses action:**
   - Restart Revit → PowerShell script manages graceful restart
   - Just Reload Runtime → Runtime reloads (Loader stays old)
   - Ignore → Can apply updates later

---

## Developer / CI suggestions

- CI should run `dotnet build` for `Rca.Runtime` and `Rca.Loader` (Release) to produce timestamped deploy folders and hashes.
- Tests or CI checks can fail if the expected `source-hash.*.txt` files are missing.
- MSBuild sends simple `BUILD_COMPLETED` signal - no complex payload needed.
- Addin autonomously determines what changed by reading metadata from DLLs.

---

## How to test/verify locally

1. Build Runtime: `dotnet build src/Rca.Runtime -c Debug`
2. Confirm new folder created in `%LOCALAPPDATA%\RCA\Runtime\<timestamp>`
3. Start Revit with RCA loaded
4. Watch for dialog prompt after build completes
5. Check Build Output window for "Build notification response: ..."
6. Test "Reload Runtime" button manually if notification didn't appear

---

## Quick links (files you will likely inspect)

- MSBuild notification: `src/Rca.Runtime/Rca.Runtime.csproj` (NotifyBuildCompleted target)
- Pipe protocol: `src/Rca.Loader/Services/PipeServerService.cs`
- Command handling: `src/Rca.Loader/Infrastructure/RuntimeCommandHandler.cs`
- User dialog: `src/Rca.Loader/Infrastructure/ShowReloadDialogHandler.cs`
- Version tracking: `src/Rca.Loader/AssemblyManagement/AssemblyStatusManager.cs`
- Runtime reload: `src/Rca.Loader/Services/RuntimeManager.cs`

This document intentionally avoids speculation and focuses on the current code. If you change the build or deployment flow, update this document accordingly.
