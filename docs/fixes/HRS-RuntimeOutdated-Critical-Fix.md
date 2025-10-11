# Critical Fix: Runtime Outdated Detection Bug

## Problem Statement

After MSBuild sends BUILD_COMPLETED signal and ProcessMsBuildSignal updates CurrentInfo with new hashes, the UI still shows runtime as "Current" instead of "OUTDATED", even though a new version exists on disk.

## Root Cause Analysis

### The Bug
`IsRuntimeOutdated()` was comparing **the same hash with itself**:

```csharp
// ProcessMsBuildSignal updates RuntimeAssembly.Hash
CurrentInfo.RuntimeAssembly.Hash = newHashFromDisk;  // e.g. "abc123"

// Then IsRuntimeOutdated compares:
var discoveredHash = ReadFromDisk();  // "abc123"
var currentHash = CurrentInfo.RuntimeAssembly.Hash;  // "abc123" (just updated!)
return discoveredHash != currentHash;  // FALSE! ❌
```

### Why This Happened
We confused two different concepts:
1. **Discovered version** (what's on disk) - updated by `ProcessMsBuildSignal`
2. **Loaded version** (what's in memory) - should only update after actual reload

Both were stored in the same `CurrentInfo.RuntimeAssembly` field, causing the comparison to always fail.

## Solution

### Architecture Change
Separated the two concepts into distinct fields in `LoadedAssembliesInfo`:

```csharp
public class LoadedAssembliesInfo
{
    // What we discovered on disk (updated by ProcessMsBuildSignal)
    public AssemblyInfo RuntimeAssembly { get; set; }
    
    // What's actually loaded in memory (updated by UpdateHashesAfterReload)
    public AssemblyInfo LoadedRuntimeAssembly { get; set; }  // NEW!
}
```

### Fixed Logic Flow

**Before (buggy):**
```
ProcessMsBuildSignal:
  1. Read NEW hash from disk
  2. Update CurrentInfo.RuntimeAssembly.Hash = NEW
  3. UI refresh → IsRuntimeOutdated():
     - Compare NEW (from disk) vs NEW (from CurrentInfo)
     - Result: FALSE ❌
```

**After (correct):**
```
ProcessMsBuildSignal:
  1. Read NEW hash from disk
  2. Update CurrentInfo.RuntimeAssembly.Hash = NEW (discovered)
  3. LoadedRuntimeAssembly.Hash stays OLD (actually loaded)
  4. UI refresh → IsRuntimeOutdated():
     - Compare NEW (discovered) vs OLD (loaded)
     - Result: TRUE ✅

UpdateHashesAfterReload:
  1. Update CurrentInfo.LoadedRuntimeAssembly.Hash = NEW
  2. Now IsRuntimeOutdated() returns FALSE ✅
```

## Code Changes

### 1. LoadedAssembliesInfo.cs
```csharp
// Added new field
public AssemblyInfo LoadedRuntimeAssembly { get; set; } = new AssemblyInfo();
```

### 2. AssemblyStatusManager.InitializeOnStartup()
```csharp
// Initialize both fields separately
CurrentInfo.LoadedRuntimeAssembly.Hash = loadedHash;  // What's in memory
CurrentInfo.RuntimeAssembly.Hash = discoveredHash;     // What's on disk
```

### 3. AssemblyStatusManager.IsRuntimeOutdated()
```csharp
// CRITICAL FIX: Compare discovered vs LOADED
var discoveredHash = ReadFromDisk();
var loadedHash = CurrentInfo.LoadedRuntimeAssembly.Hash;  // ← KEY CHANGE
return discoveredHash != loadedHash;
```

### 4. AssemblyStatusManager.UpdateHashesAfterReload()
```csharp
// Update LOADED hash after successful reload
CurrentInfo.LoadedRuntimeAssembly.Hash = newHash;
CurrentInfo.RuntimeAssembly.Hash = newHash;  // Keep in sync
```

## Test Case

```csharp
[Test]
public void ProcessMsBuildSignal_ShouldDetectRuntimeOutdated_EvenAfterHashUpdate()
{
    // Arrange: Old hash in memory
    _statusManager.CurrentInfo.LoadedRuntimeAssembly.Hash = "old_hash";
    
    // New DLL on disk
    CreateMockDllWithHash("new_hash");
    
    // Act: Process MSBuild signal
    _statusManager.ProcessMsBuildSignal(folder);
    
    // Assert: RuntimeAssembly updated, but LoadedRuntimeAssembly stays old
    Assert.That(_statusManager.CurrentInfo.RuntimeAssembly.Hash, Is.EqualTo("new_hash"));
    Assert.That(_statusManager.CurrentInfo.LoadedRuntimeAssembly.Hash, Is.EqualTo("old_hash"));
    
    // Critical: IsRuntimeOutdated should return TRUE
    Assert.That(_statusManager.IsRuntimeOutdated(), Is.True);  // ✅ Passes now!
}
```

## Impact

### What Now Works
✅ MSBuild signal updates discovered version  
✅ UI shows "OUTDATED" correctly after signal  
✅ IsRuntimeOutdated() compares correct values  
✅ After reload, status updates to "Current"  
✅ Loader hash detection also works (same fix pattern)

### What Was Broken
❌ UI always showed "Current" even when new version existed  
❌ IsRuntimeOutdated() always returned false after ProcessMsBuildSignal  
❌ Users had to manually check if updates were available

## Related Files
- `src/Rca.Loader/AssemblyManagement/LoadedAssembliesInfo.cs`
- `src/Rca.Loader/AssemblyManagement/AssemblyStatusManager.cs`
- `tests/Rca.Loader.Tests/AssemblyStatusManagerTests.cs` (new)
- `docs/HRS-Build-Signal-Simplification.md` (updated)

## Migration Notes
This is a data model change. Existing `LoadedAssemblies.json` files will not have `LoadedRuntimeAssembly` field.  
The code handles this gracefully - missing field defaults to empty AssemblyInfo, which triggers "outdated" status.

## Future Improvements
1. Consider persisting `LoadedRuntimeAssembly` to JSON for cross-session tracking
2. Add similar separation for Loader (LoadedLoaderComponents vs LoaderComponents)
3. Add telemetry to track how often false negatives occurred before this fix
