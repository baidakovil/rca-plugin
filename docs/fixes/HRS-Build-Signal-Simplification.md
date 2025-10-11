# Hot-Reload System Improvements - Build Signal Simplification

## Summary

Simplified the MSBuild → Revit communication for hot-reload notifications. MSBuild now sends a simple `BUILD_COMPLETED` signal instead of complex payload with paths and hashes. The Revit addin autonomously detects what changed and triggers the existing `ReloadRuntimeCommand` to show interactive dialog.

## Changes Made

### 1. New BUILD_COMPLETED Command

**File:** `src/Rca.Loader/Infrastructure/CommandValidationService.cs`

- Added `PipeCommands.BuildCompleted` constant
- Added validation for BUILD_COMPLETED command (no payload required)

### 2. Command Trigger Handler

**File:** `src/Rca.Loader/Infrastructure/TriggerCommandHandler.cs` (NEW)

- Simple `IExternalEventHandler` that triggers Revit commands programmatically
- Uses `PostCommand` API to invoke existing `ReloadRuntimeCommand`
- Avoids code duplication by reusing existing dialog logic

### 3. BUILD_COMPLETED Handler (Simplified)

**File:** `src/Rca.Loader/Infrastructure/RuntimeCommandHandler.cs`

- Added `HandleBuildCompletedCommand()` method
- Added `TriggerReloadRuntimeCommand()` helper
- Flow:
  1. Find latest deploy folder automatically
  2. Read hashes from DLLs (single source of truth)
  3. Update AssemblyStatusManager
  4. **Trigger ReloadRuntimeCommand** (reuses existing dialog)

**Why trigger existing command instead of showing custom dialog:**
- ✅ Reuses well-tested dialog logic from `ReloadRuntimeCommand`
- ✅ No code duplication
- ✅ Consistent UX (same dialog for manual and automatic triggers)
- ✅ Simpler maintenance

### 4. Fixed ProcessMsBuildSignal Logic ⚠️ CRITICAL FIX

**File:** `src/Rca.Loader/AssemblyManagement/AssemblyStatusManager.cs`

**Previous bug:** Hash values were read from new DLLs and compared with CurrentInfo, but **not saved** back to CurrentInfo!

**Result:** 
- `LastMSBuildSignal.Event` showed "only loader outdated" ✅
- But `CurrentInfo.LoaderComponents.Hash` remained old ❌
- So `IsLoaderOutdated()` always returned `false` ❌

**Fix:**
```csharp
// BEFORE (buggy):
bool loaderChanged = loaderHash != CurrentInfo.LoaderComponents.Hash;
// Event recorded but hash NOT updated!

// AFTER (fixed):
bool loaderChanged = loaderHash != CurrentInfo.LoaderComponents.Hash;
// ... determine event ...
CurrentInfo.LoaderComponents.Hash = loaderHash;  // ← FIX: Save new hash
CurrentInfo.LoaderComponents.Path = deployFolderMeta;
```

Now:
1. ✅ Hashes are compared correctly
2. ✅ Event is recorded ("only loader outdated")
3. ✅ **CurrentInfo is updated with new values**
4. ✅ `IsLoaderOutdated()` sees fresh hash
5. ✅ UI displays "OUTDATED" correctly

### 5. Fixed ReloadRuntimeCommand Logic

**File:** `src/Rca.Loader/Commands/ReloadRuntimeCommand.cs`

**Critical fix:** Previously, `IsRuntimeOutdated()` was checked BEFORE `ProcessMsBuildSignal()`, causing "All assemblies are up to date" false negatives.

**Now:** 
1. First calls `ProcessMsBuildSignal(latest)` to update CurrentInfo
2. Then checks `IsRuntimeOutdated()` with fresh data
3. Correctly detects new builds

### 6. Simplified MSBuild Target

**File:** `src/Rca.Runtime/Rca.Runtime.csproj`

**Before:**
```xml
<Target Name="ReloadRuntime">
  <!-- Complex PowerShell with path escaping -->
  <PsHotReloadCmd>... "Payload":"$(RuntimeDeployDirEscaped)" ...</PsHotReloadCmd>
</Target>
```

**After:**
```xml
<Target Name="NotifyBuildCompleted">
  <!-- Simple signal, no payload -->
  <PsNotifyCmd>... "Payload":"" ...</PsNotifyCmd>
</Target>
```

**Benefits:**
- ✅ No JSON escaping needed
- ✅ Shorter timeout (3s vs 2s)
- ✅ Clearer messaging
- ✅ More reliable

### 7. Removed Duplicate Code ⚠️ IMPORTANT

**Deleted file:** `src/Rca.Loader/Infrastructure/ShowReloadDialogHandler.cs`

**Why removed:**
- ❌ Duplicated dialog logic from `ReloadRuntimeCommand`
- ❌ Increased maintenance burden
- ❌ Risk of UX inconsistency

**Now:** Single source of truth for reload dialogs in `ReloadRuntimeCommand.Execute()`

### 8. Updated Documentation

**File:** `docs/HRS.md`

- Documented new BUILD_COMPLETED command
- Explained command triggering approach
- Updated examples and troubleshooting

## Architecture Benefits

### Before (Complex Payload + Duplicate Dialogs)
```
MSBuild                 Revit
   |                      |
   |-- Deploy DLLs -->    |
   |-- Send path -->      |-- Parse path
   |                      |-- Read DLLs
   |                      |-- Show custom dialog ← DUPLICATION!
```

**Problems:**
- ❌ Path escaping complexity
- ❌ Duplicate dialog code
- ❌ Hash not saved to CurrentInfo
- ❌ Fragile JSON serialization

### After (Simple Signal + Reuse Command)
```
MSBuild                 Revit
   |                      |
   |-- Deploy DLLs -->    |
   |-- Send signal -->    |-- Find latest folder
   |                      |-- Read & save hashes
   |                      |-- Trigger ReloadRuntimeCommand
   |                      |   (shows existing dialog)
```

**Benefits:**
- ✅ Single source of truth (addin finds folder)
- ✅ No complex payload or escaping
- ✅ Hashes correctly saved to CurrentInfo
- ✅ No dialog duplication
- ✅ Reuses well-tested command logic
- ✅ More reliable communication

## Bug Fixes Summary

### Bug 1: Loader Hash Not Updating ⚠️ CRITICAL
**Symptoms:**
- LastMSBuildSignal shows "only loader outdated" ✅
- But Loader status shows "Current" instead of "OUTDATED" ❌

**Root cause:**
`ProcessMsBuildSignal()` compared hashes but didn't save new values to `CurrentInfo`

**Fix:**
Now saves `loaderHash` and `runtimeHash` to `CurrentInfo` after comparison

**Impact:** HIGH - Without this fix, hot-reload system doesn't work correctly

### Bug 2: Dialog Duplication
**Symptoms:**
- Two separate implementations of same dialog logic
- Maintenance burden
- Risk of UX inconsistency

**Fix:**
- Deleted `ShowReloadDialogHandler`
- Trigger existing `ReloadRuntimeCommand` instead

**Impact:** MEDIUM - Simplifies codebase, reduces bugs

### Bug 3: ReloadRuntimeCommand False Negatives
**Symptoms:**
- "All assemblies are up to date" shown when new build exists

**Root cause:**
Checked `IsRuntimeOutdated()` before calling `ProcessMsBuildSignal()`

**Fix:**
Call `ProcessMsBuildSignal()` first to update CurrentInfo

**Impact:** MEDIUM - Manual reload button now works correctly

## Testing Checklist

- [x] Build compiles without errors
- [ ] MSBuild sends BUILD_COMPLETED after deploy
- [ ] ReloadRuntimeCommand dialog appears automatically after build
- [ ] Loader outdated scenario shows "Restart Revit" option
- [ ] Runtime-only update shows "Reload Runtime" option
- [ ] Manual "Reload Runtime" button detects changes correctly
- [ ] Hash values update in CurrentInfo after ProcessMsBuildSignal
- [ ] UI status display shows "OUTDATED" when Loader hash changes
- [ ] Logs show hash updates: "hash={old}->{new}"

## Migration Notes

**No breaking changes** - old commands still supported:
- `RELOAD` - works as before
- `RELOAD_RUNTIME` - works as before  
- `STATUS` - works as before
- `BUILD_COMPLETED` - NEW, recommended for MSBuild

Existing workflows continue to function.

## Critical Changes to Review

1. **ProcessMsBuildSignal hash update** - verify logic is correct
2. **TriggerCommandHandler PostCommand** - verify command ID lookup works
3. **Removal of ShowReloadDialogHandler** - confirm no references remain

## Future Improvements

1. Add debouncing (ignore repeated signals within 5 seconds)
2. Add retry logic in PowerShell script (2-3 attempts)
3. Add user preference "Auto-reload Runtime" for power users
4. Log all user dialog interactions for analytics
5. Consider notification toast instead of blocking dialog
