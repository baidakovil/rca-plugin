# Hot-Reload Logging Contracts Fix

## Problem

Revit was locking `Rca.Logging.Contracts.dll` file in the temp directory, which prevented Hot-Reload from working correctly. The file remained locked by Revit even after attempting to reload the runtime.

## Root Cause

`Rca.Logging.Contracts.dll` was being deployed as a separate file to the hot-reload directory and loaded by the Runtime. When Revit loaded this DLL, it locked the file, preventing MSBuild from overwriting it during subsequent builds.

## Solution

**Merged `Rca.Logging.Contracts` into `Rca.Loader.dll`** to avoid file locking issues. The Loader assembly is loaded once at Revit startup and is not reloaded during Hot-Reload, so there are no file locking conflicts.

### Changes Made

1. **Rca.Loader.csproj**:
   - Added `Rca.Logging.Contracts.dll` to ILRepack merge list
   - Added `Rca.Logging.Contracts` namespace to internalize exclude list (keep types public)
   - Updated SourceHash to include `Rca.Logging.Contracts` source files

2. **Rca.Runtime.csproj**:
   - Removed `ProjectReference` to `Rca.Logging.Contracts`
   - Added `extern alias LoaderMerged` to Rca.Loader reference
   - Added target to exclude `Rca.Logging.Contracts.dll` from being copied to output
   - Removed `Rca.Logging.Contracts.dll` from runtime deployment files

3. **Runtime Source Files**:
   - Added `extern alias LoaderMerged` to files using Logging.Contracts types:
     - `RuntimeEntry.cs`
     - `NamedPipeLoggerProvider.cs`
     - `RuntimePanelFactory.cs`
   - Changed usings to: `using LoaderMerged::Rca.Logging.Contracts;`

4. **Rca.UI.csproj and UiLog.cs**:
   - **Removed** `ProjectReference` to `Rca.Logging.Contracts` from UI project
   - **Refactored** `UiLog.cs` to use anonymous type instead of `LogEntryDto`
   - This eliminates runtime dependency on Logging.Contracts assembly
   - UiLog now works as standalone logging adapter without external dependencies

5. **Solution File**:
   - Added `Rca.Logging.Contracts.csproj` to solution for proper build order

6. **Logger Provider Sharing**:
   - Added `GetLoggerProvider()` method to `RuntimeEntry` to share the logger provider instance
   - Updated `RuntimePanelFactory` to use shared logger provider for consistent logging
   - Fixed issue where RuntimePanelFactory had separate SessionId causing logging disconnection

## SourceHash Calculation

### Loader
**Includes**: `Rca.Loader` + `Rca.Loader.Contracts` + `Rca.Logging.Contracts`

Hash changes when any of these projects' source files change, triggering Loader reload (Revit restart required).

### Runtime
**Includes**: `Rca.Runtime` + `Rca.Core` + `Rca.UI` + `Rca.Network` + `Rca.Contracts`

**Excludes**: `Rca.Logging.Contracts` (now part of Loader)

Hash changes when runtime components change, triggering Hot-Reload without Revit restart.

## Benefits

1. **No file locking**: `Rca.Logging.Contracts.dll` is no longer deployed as separate file
2. **Cleaner deployment**: Fewer DLL files in hot-reload directory
3. **Consistent types**: All logging contract types come from single source (Rca.Loader.dll)
4. **Proper Hot-Reload**: Runtime can be reloaded without file access conflicts
5. **No UI assembly dependency**: UI logging works without loading Logging.Contracts assembly
6. **Unified logging**: All Runtime components share single logger provider and SessionId

## Technical Details

### Assembly Loading Strategy

- **Loader**: Loads once at Revit startup, contains merged Logging.Contracts
- **Runtime**: Loads in collectible AssemblyLoadContext, references Loader for Logging.Contracts types via `extern alias`
- **UI**: Uses inline anonymous type for logging DTO, no dependency on Logging.Contracts assembly
- **Type Resolution**: Runtime uses `extern alias` to explicitly reference types from merged Loader assembly

### Type Ambiguity Resolution

The `extern alias LoaderMerged` pattern resolves potential type ambiguity:
- Prevents conflicts between Logging.Contracts types in different assemblies
- Makes it explicit which assembly provides the types
- Avoids CS0433 compiler errors about duplicate type definitions

### UI Logging Implementation

`UiLog.cs` uses an anonymous type that matches `LogEntryDto` structure:
```csharp
var dto = new
{
    SchemaVersion = "1",
    TimestampTicks = DateTime.Now.Ticks,
    Level = logLevel.ToString(),
    Category = _category,
    Message = msg,
    Exception = exception?.ToString(),
    RuntimeSessionId = SessionId,
    SequenceId = Interlocked.Increment(ref _seq),
    RuntimeProcessId = Environment.ProcessId,
    IsFallback = false,
    Flags = 0,
    IsPing = false
};
```

This approach:
- Avoids compile-time and runtime dependency on Logging.Contracts
- Prevents assembly loading issues when UI is merged into Runtime
- Maintains compatibility with logging infrastructure through JSON serialization
- Keeps UI project lightweight and self-contained

## Migration Notes

If you encounter issues after this change:

1. **Clean and rebuild**: `dotnet clean && dotnet build --no-incremental`
2. **Delete hot-reload folders**: Remove `%LocalAppData%\RCA\Runtime\*`
3. **Restart Revit**: Ensure old Loader version is unloaded
4. **Check logs**: Look for logging connection issues in `%LocalAppData%\RCA\Logs\`
5. **Verify no Logging.Contracts.dll**: Ensure `Rca.Logging.Contracts.dll` is NOT in hot-reload directory

## Related Files

- `src/Rca.Loader/Rca.Loader.csproj` - ILRepack configuration
- `src/Rca.Runtime/Rca.Runtime.csproj` - Reference and exclusion configuration
- `src/Rca.Runtime/RuntimeEntry.cs` - Shared logger provider
- `src/Rca.Runtime/UI/RuntimePanelFactory.cs` - Logger provider usage
- `src/Rca.Runtime/Logging/NamedPipeLoggerProvider.cs` - Logging implementation
- `src/Rca.UI/Rca.UI.csproj` - Removed Logging.Contracts dependency
- `src/Rca.UI/Logging/UiLog.cs` - Standalone logging adapter with inline DTO
