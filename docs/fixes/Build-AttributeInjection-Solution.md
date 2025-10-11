# Build Attribute Injection Solution

## Problem Statement

When using ILRepack to merge assemblies in the RCA plugin, MSBuild caching caused inconsistent assembly attributes across different deployment locations. Specifically:

- Different versions of `Rca.Loader.dll` were copied to addin and temp folders
- Assembly metadata attributes didn't match the source hash in version files
- MSBuild incremental builds restored old cached files before AttributeInjector could run

## Root Cause Analysis

### 1. Multiple Copy Sources
**Problem:** Different MSBuild targets copied from different source paths:
- `InitialSetupCopyToRevitAddins` used `$(OutputPath)Rca.Loader.dll`
- `DeployLoaderToTemp` used `$(TargetPath)`

With `BaseOutputPath` configured, these could point to different file locations, causing version mismatches.

### 2. MSBuild Cache Restoration
**Problem:** MSBuild caches intermediate files in `obj/` folder:
- During incremental builds, MSBuild could restore old DLL from cache
- This happened BEFORE AttributeInjector modified the file
- `Touch` task updated timestamp, but file was already stale

### 3. Build Order Ambiguity
**Problem:** Unclear dependency chain between targets:
- `InitialSetupCopyToRevitAddins` had `DependsOnTargets="InjectLoaderAttributes"`
- But also used `AfterTargets="DeployLoaderToTemp"` 
- Could execute before attribute injection completed in edge cases

## Solution Implementation

### 1. Unified Copy Source (✅ Implemented)

**File:** `build/Common.targets`

Changed `InitialSetupCopyToRevitAddins` to always use `$(TargetPath)`:

```xml
<!-- Before: -->
<Copy SourceFiles="$(OutputPath)Rca.Loader.dll" ... />

<!-- After: -->
<Copy SourceFiles="$(TargetPath)" ... />
```

**Why:** `$(TargetPath)` is the file modified by AttributeInjector, guaranteed to have correct metadata.

### 2. Cache Cleanup (✅ Implemented)

**File:** `src/Rca.Loader/Rca.Loader.csproj`

Added cache cleanup in `InjectLoaderAttributes` target:

```xml
<Target Name="InjectLoaderAttributes" AfterTargets="BuildAttributeInjector">
  <!-- Execute AttributeInjector -->
  <Exec Command="..." />
  
  <!-- Update file timestamp -->
  <Touch Files="$(TargetPath)" AlwaysCreate="false" />
  
  <!-- Clean intermediate cache -->
  <ItemGroup>
    <FilesToClean Include="$(IntermediateOutputPath)$(TargetFileName)" />
    <FilesToClean Include="$(IntermediateOutputPath)**\$(TargetFileName)" />
  </ItemGroup>
  <Delete Files="@(FilesToClean)" ContinueOnError="true" />
</Target>
```

**Why:** Prevents MSBuild from using cached pre-injection files in subsequent builds.

### 3. Fixed Runtime HintPath (✅ Implemented)

**File:** `src/Rca.Runtime/Rca.Runtime.csproj`

Updated Loader reference to use unified output path:

```xml
<!-- Before: -->
<HintPath>..\Rca.Loader\bin\$(Configuration)\net8.0-windows\Rca.Loader.dll</HintPath>

<!-- After: -->
<HintPath>..\..\bin\$(Configuration)\$(TargetFramework)\Rca.Loader.dll</HintPath>
```

**Why:** With `BaseOutputPath=$(SolutionDir)bin\`, all projects build to same directory.

### 4. Correct Build Order

Final build sequence for Loader:

```
1. GenerateLoaderSourceHash (BeforeTargets="CoreCompile")
   └─ Compute hash → source-hash-loader.txt

2. CoreCompile
   └─ Compile Rca.Loader.dll (no attributes yet)

3. RepackLoader (AfterTargets="Build")
   └─ Merge Rca.Loader.dll + Rca.Loader.Contracts.dll

4. BuildAttributeInjector (AfterTargets="RepackLoader")
   └─ Compile AttributeInjector if outdated

5. InjectLoaderAttributes (AfterTargets="BuildAttributeInjector")
   ├─ Execute AttributeInjector
   ├─ Touch file timestamp
   └─ Delete cache files

6. DeployLoaderToTemp (AfterTargets="InjectLoaderAttributes")
   ├─ Copy $(TargetPath) → temp folder
   └─ Write SourceHash-Loader-{hash}.txt

7. InitialSetupCopyToRevitAddins (AfterTargets="DeployLoaderToTemp", DependsOnTargets="InjectLoaderAttributes")
   └─ Copy $(TargetPath) → addin folder (only if not exists)
```

## Verification Results

### Test Scenario 1: Clean Build
```powershell
dotnet build src/Rca.Loader/Rca.Loader.csproj -c Debug --no-incremental
```

**Expected:** All files have identical SourceHash metadata
**Result:** ✅ PASS

| Location | SourceHash | DeployFolder |
|----------|------------|--------------|
| Build folder | c7df5f | 20251004_145629 |
| Temp folder | c7df5f | 20251004_145629 |
| Addin folder | c7df5f | 20251004_145629 |
| Version file | c7df5f | - |

**Note:** Windows Explorer Properties shows "1.0.0.0" for Product Version because it reads native Win32 resources, not .NET managed attributes. This is expected - our runtime code uses Reflection to read managed attributes (AssemblyInformationalVersion, AssemblyMetadata) which contain correct SourceHash and DeployFolder values.

### Test Scenario 2: Incremental Build After Code Change
```powershell
# Modify LoaderApp.cs
dotnet build src/Rca.Loader/Rca.Loader.csproj -c Debug
```

**Expected:** Build folder and temp folder updated, addin unchanged
**Result:** ✅ PASS

| Location | SourceHash | Behavior |
|----------|------------|----------|
| Build folder | 7b81f7 | Updated ✅ |
| Temp folder | 7b81f7 | Updated ✅ |
| Addin folder | c7df5f | Not updated (expected) ✅ |

### Test Scenario 3: Runtime Build
```powershell
dotnet build src/Rca.Runtime/Rca.Runtime.csproj -c Debug --no-incremental
```

**Expected:** Runtime DLL and version file have matching hashes
**Result:** ✅ PASS

| Item | SourceHash |
|------|------------|
| Rca.Runtime.dll | dc0c53 |
| SourceHash-Runtime-dc0c53.txt | dc0c53 |

### Test Scenario 4: Initial Setup
```powershell
Remove-Item "$env:APPDATA\Autodesk\Revit\Addins\2026\Rca\Rca.Loader.dll"
dotnet build src/Rca.Loader/Rca.Loader.csproj -c Debug
```

**Expected:** Addin file copied with correct attributes
**Result:** ✅ PASS

```
InitialSetup check: IsLoaderProject=true, AddinExists=False, WillCopy=true
Performed initial setup - copied Loader assembly from <TargetPath>
```

## Diagnostic Commands

### Inspect Assembly Attributes
```powershell
& "src\Tools\AttributeInjector\bin\Debug\net8.0\AttributeInjector.exe" inspect <path-to-dll>
```

### Compare All Locations
```powershell
$latest = Get-ChildItem "$env:LOCALAPPDATA\RCA\Runtime" -Directory | 
          Sort-Object LastWriteTime -Descending | 
          Select-Object -First 1

Write-Host "Build:" 
AttributeInjector inspect "bin\Debug\net8.0-windows\Rca.Loader.dll"

Write-Host "`nTemp:"
AttributeInjector inspect "$($latest.FullName)\Rca.Loader.dll"

Write-Host "`nAddin:"
AttributeInjector inspect "$env:APPDATA\Autodesk\Revit\Addins\2026\Rca\Rca.Loader.dll"
```

### Verify Build Order
```powershell
dotnet build -v:detailed | Select-String "Target.*Loader"
```

## Files Modified

1. **src/Rca.Loader/Rca.Loader.csproj**
   - Updated `InjectLoaderAttributes` target to clean cache
   - Removed unified output path deletion (not needed)

2. **build/Common.targets**
   - Changed `InitialSetupCopyToRevitAddins` to use `$(TargetPath)`
   - Added diagnostic messages

3. **src/Rca.Runtime/Rca.Runtime.csproj**
   - Fixed `HintPath` to point to unified output directory
   - Already had cache cleanup in `InjectRuntimeAttributes`

4. **.github/prompting/problem.md**
   - Updated with final solution documentation

## Best Practices Going Forward

### Do ✅
- Always copy from `$(TargetPath)` after attribute injection
- Clean intermediate cache after modifying DLL post-build
- Use `--no-incremental` for release builds
- Verify attributes match version files in CI/CD

### Don't ❌
- Don't copy from `$(OutputPath)` - may be stale
- Don't delete unified bin/ folder - breaks other projects
- Don't rely on ILRepack `/copyattrs` - doesn't work for custom attributes
- Don't auto-update addin folder - requires Revit restart

## Troubleshooting Guide

### Problem: Attributes don't match between locations

**Check:**
1. Build order: `dotnet build -v:detailed | Select-String "Target"`
2. AttributeInjector executed: `dotnet build | Select-String "Injecting"`
3. File timestamps: `Get-Item <dll> | Select-Object LastWriteTime`
4. Cache files: `Get-ChildItem "src\Rca.Loader\obj" -Recurse -Filter "*.dll"`

### Problem: Build fails with "Could not copy file"

**Solution:**
- Check `DependsOnTargets` - all dependencies must be explicit
- Use `ContinueOnError="true"` for Delete tasks
- Verify source file exists before copy operation

### Problem: HintPath not found

**Solution:**
- Use relative path from project file location
- Use `$(TargetFramework)` instead of hardcoded framework
- Verify `BaseOutputPath` configuration

## Related Documentation

- [HRS.md](HRS.md) - Hot-Reload System overview
- [HRS-ILRepack.md](HRS-ILRepack.md) - ILRepack build architecture
- [HRS-Hash-System.md](HRS-Hash-System.md) - Source hash generation
- [AttributeInjector source](../src/Tools/AttributeInjector/Program.cs)

## Summary

The solution ensures consistent assembly attributes across all deployment locations by:
1. Using a single, reliable copy source (`$(TargetPath)`)
2. Cleaning MSBuild cache after attribute injection
3. Maintaining correct build order dependencies
4. Fixing assembly reference paths to match unified output structure

All tests pass, and the system now reliably produces matching attributes in build, temp, and addin locations.
