# Hot-Reload System Build Consistency Issue

This document describes the MSBuild caching issue discovered during implementation of build consistency tests and provides solutions for future resolution.

## Problem Summary

**Symptom:** Assembly metadata attributes injected by `AttributeInjector` tool are overwritten with old values from MSBuild cache during incremental builds.

**Impact:** Build consistency tests fail because `Rca.Loader.dll` in deploy folder contains outdated `DeployFolder` and `SourceHash` metadata.

## Root Cause Analysis

### Build Process Flow

1. **Initial Build:**
   ```
   CoreCompile → Build → RepackLoader → InjectLoaderAttributes → DeployLoaderToTemp
   ```
   - CoreCompile creates `Rca.Loader.dll` in `bin/Debug/net8.0-windows/`
   - MSBuild caches intermediate files in `obj/Debug/net8.0-windows/`
   - RepackLoader merges assemblies using ILRepack
   - InjectLoaderAttributes modifies DLL with Mono.Cecil (post-build)
   - DeployLoaderToTemp copies modified DLL to deploy folder

2. **Subsequent Build (Incremental):**
   ```
   MSBuild detects "no changes" → Restores DLL from obj cache → Skips AttributeInjector
   ```
   - MSBuild uses cached version from `obj/` folder
   - Cached version has OLD metadata from previous build
   - AttributeInjector never runs (target skipped as "up-to-date")

### Why This Happens

**MSBuild Incremental Build Logic:**
- MSBuild tracks file modification times to determine what needs rebuilding
- Post-build modifications (AttributeInjector) happen AFTER MSBuild's dependency graph is evaluated
- Modified DLL has NEWER timestamp than source files
- Next build: MSBuild thinks nothing changed, restores OLD version from cache

**Evidence:**
```powershell
# After clean build - CORRECT attributes
AttributeInjector inspect bin/Debug/net8.0-windows/Rca.Loader.dll
# Output: DeployFolder = 20251004_142718, SourceHash = d9f829

# After incremental build (e.g., via dotnet test) - OLD attributes restored!
AttributeInjector inspect bin/Debug/net8.0-windows/Rca.Loader.dll  
# Output: DeployFolder = 20251004_135054, SourceHash = 0a3746
```

## Attempted Solutions (Did Not Work)

### 1. File.Move Instead of File.Copy in AttributeInjector ❌
**Attempted:** Replace `File.Copy` with atomic `File.Delete + File.Move` operation
**Result:** Did not solve MSBuild cache issue
**File:** `src/Tools/AttributeInjector/Program.cs`

### 2. Explicit DependsOnTargets in DeployLoaderToTemp ❌
**Attempted:** Add `DependsOnTargets="InjectLoaderAttributes"` to ensure proper ordering
**Result:** Ordering was already correct; caching issue persists
**File:** `src/Rca.Loader/Rca.Loader.csproj`

### 3. Cleaning obj Folders Before Build ⚠️
**Attempted:** Delete all `obj/` folders in build wrapper script
**Result:** Prevents caching but breaks incremental builds (slow)
**File:** `build.ps1`

## Recommended Solutions

### Solution 1: Source Code Generation (RECOMMENDED) ✅

**Approach:** Generate assembly metadata as C# source code BEFORE compilation instead of post-build injection.

**Implementation Plan:**

1. **Create MSBuild target `GenerateAssemblyMetadata` in Rca.Loader.csproj:**
   ```xml
   <Target Name="GenerateAssemblyMetadata" BeforeTargets="CoreCompile" 
           DependsOnTargets="GenerateLoaderSourceHash">
     <PropertyGroup>
       <MetadataFile>$(IntermediateOutputPath)RcaLoaderAssemblyMetadata.g.cs</MetadataFile>
     </PropertyGroup>
     
     <WriteLinesToFile File="$(MetadataFile)" Lines="
using System.Reflection;

[assembly: AssemblyMetadata(&quot;DeployFolder&quot;, &quot;$(RcaHotReloadTimestamp)&quot;)]
[assembly: AssemblyMetadata(&quot;SourceHash&quot;, &quot;$(RcaLoaderSourceHash)&quot;)]
[assembly: AssemblyInformationalVersion(&quot;DeployFolder: $(RcaHotReloadTimestamp), SourceHash: $(RcaLoaderSourceHash)&quot;)]
" Overwrite="true" />
     
     <ItemGroup>
       <Compile Include="$(MetadataFile)" />
     </ItemGroup>
   </Target>
   ```

2. **Remove post-build AttributeInjector targets:**
   - Delete `BuildAttributeInjector` target
   - Delete `InjectLoaderAttributes` target

3. **Update DeployLoaderToTemp:**
   - Remove `DependsOnTargets="InjectLoaderAttributes"`
   - Attributes now embedded during compilation

**Advantages:**
- ✅ Attributes compiled into DLL, no post-build modification
- ✅ MSBuild incremental build works correctly
- ✅ ILRepack `/copyattrs` flag copies attributes automatically
- ✅ No Mono.Cecil dependency
- ✅ Faster builds (no post-processing)

**Disadvantages:**
- ⚠️ Generated code adds one more file to track
- ⚠️ Requires careful escaping in WriteLinesToFile

### Solution 2: Disable Incremental Build for Loader Project ⚠️

**Approach:** Force full rebuild of Loader every time.

**Implementation:**
```xml
<!-- Rca.Loader.csproj -->
<PropertyGroup>
  <DisableFastUpToDateCheck>true</DisableFastUpToDateCheck>
</PropertyGroup>

<Target Name="ForceLoaderRebuild" BeforeTargets="CoreCompile">
  <Delete Files="$(TargetPath)" />
</Target>
```

**Advantages:**
- ✅ Simple to implement
- ✅ Guarantees AttributeInjector always runs

**Disadvantages:**
- ❌ Slower builds (no incremental compilation)
- ❌ Doesn't solve root cause
- ❌ Wastes developer time on unnecessary rebuilds

### Solution 3: Custom MSBuild Task for Metadata Injection 🔧

**Approach:** Create custom MSBuild task that runs DURING compilation phase, not after.

**Implementation:**
1. Create `InjectMetadataTask.cs` in `src/Rca.Loader/BuildTasks/`
2. Use Mono.Cecil to modify assembly DURING `AfterCompile` phase
3. Update MSBuild file timestamps to invalidate cache

**Advantages:**
- ✅ Metadata injection integrated into build process
- ✅ MSBuild tracks dependencies correctly
- ✅ Preserves incremental builds

**Disadvantages:**
- ❌ Complex implementation
- ❌ Requires deep MSBuild knowledge
- ❌ May have race conditions with ILRepack

## Current Workarounds

### For Local Development

Use `build.ps1` wrapper script with obj folder cleaning:
```powershell
.\build.ps1  # Cleans obj folders, ensures consistent timestamp
```

### For CI/CD

Always use clean builds:
```bash
dotnet clean
dotnet build --no-incremental
```

## Test Results Status

**File:** `tests/Rca.Build.Tests/BuildConsistencyTests.cs`

```
Test Run Failed.
Total tests: 3
  ✅ Passed: 1 (RuntimeDll_SourceHashMetadata_MatchesVersionFile)
  ❌ Failed: 2 (LoaderDll_SourceHashMetadata_MatchesVersionFile, 
                 DeployedDlls_DeployFolderMetadata_MatchesFolderName)
```

**Why Runtime test passes but Loader test fails:**
- Runtime uses same AttributeInjector approach but different build sequence
- Runtime is built AFTER Loader in solution build order
- Runtime doesn't have as many dependent projects triggering rebuilds

## Files Modified During Investigation

### Created Files
1. `tests/Rca.Build.Tests/Rca.Build.Tests.csproj` - Test project
2. `tests/Rca.Build.Tests/BuildConsistencyTests.cs` - Consistency tests
3. `build.ps1` - Build wrapper script with timestamp management
4. `docs/HRS-BuildConsistencyIssue.md` - This document

### Modified Files
1. `build/Common.targets` - Fixed InitialSetupCopyToRevitAddins dynamic condition
2. `Directory.Build.props` - Added RCA_BUILD_TIMESTAMP environment variable support
3. `src/Rca.Loader/Rca.Loader.csproj` - Added DependsOnTargets to DeployLoaderToTemp
4. `src/Tools/AttributeInjector/Program.cs` - Improved file replacement logic

## Next Steps

**Priority 1:** Implement Solution 1 (Source Code Generation)
- Generate `RcaLoaderAssemblyMetadata.g.cs` before compilation
- Remove AttributeInjector from Loader build process
- Apply same approach to Runtime project

**Priority 2:** Update Documentation
- Document new build process in HRS-ILRepack.md
- Update developer guide with new build requirements

**Priority 3:** Verify Tests Pass
- Run `build.ps1` and confirm all 3 tests pass
- Add CI check to prevent regression

## References

- MSBuild Incremental Build: https://learn.microsoft.com/en-us/visualstudio/msbuild/incremental-builds
- MSBuild Targets: https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-targets
- Source Generators: https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview
- ILRepack: https://github.com/gluck/il-repack

---

**Document Status:** Active Issue
**Created:** 2025-10-04
**Last Updated:** 2025-10-04
**Severity:** Medium (tests fail, but workaround exists)
**Assigned To:** Future AI Agent / Developer
