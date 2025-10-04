# ILRepack Build System Architecture

This document describes the ILRepack-based assembly merging strategy used in the Hot-Reloading System and its integration with the build pipeline.

## Overview

The RCA plugin uses **ILRepack** to merge multiple assemblies into single deployment units, reducing file count and simplifying the hot-reload mechanism. Two assemblies are merged:

1. **Rca.Loader.dll** ← merges `Rca.Loader` + `Rca.Loader.Contracts`
2. **Rca.Runtime.dll** ← merges `Rca.Runtime` + `Rca.Core` + `Rca.UI` + `Rca.Network` + `Rca.Contracts`

## Why ILRepack?

### Benefits
1. **Single-file deployment** - Fewer DLLs to copy and manage during hot-reload
2. **Reduced type identity conflicts** - All types in one assembly = no duplicate type issues across load contexts
3. **Cleaner hot-reload** - Only two assemblies to track instead of 7+
4. **Simpler deployment** - Fewer files in Revit addin folder and runtime deploy folder

### Challenges Addressed
1. **Revit API references** - Must not be merged, handled via `/lib` parameter
2. **Type visibility** - Contract types must remain public for cross-context access
3. **Assembly attributes** - Must be preserved after merge via `AttributeInjector`
4. **Build order** - Loader must build before Runtime (Runtime references merged Loader.dll)

## Build Architecture

### High-Level Build Flow

```
┌─────────────────────────────────────────────────────────────┐
│  Solution Build Order (managed by MSBuild dependencies)     │
├─────────────────────────────────────────────────────────────┤
│  1. Rca.Contracts                                           │
│  2. Rca.Logging.Contracts                                   │
│  3. Rca.Loader.Contracts                                    │
│  4. Rca.Core, Rca.UI, Rca.Network (parallel)               │
│  5. Rca.Loader ──► ILRepack ──► Merged Rca.Loader.dll      │
│  6. Rca.Runtime ──► ILRepack ──► Merged Rca.Runtime.dll    │
└─────────────────────────────────────────────────────────────┘
```

### Loader Build Pipeline

**File:** `src/Rca.Loader/Rca.Loader.csproj`

```
1. BuildSourceHashGenerator (BeforeTargets="GenerateLoaderSourceHash")
   └─ Compile SourceHashGenerator tool (incremental - only if outdated)

2. GenerateLoaderSourceHash (BeforeTargets="CoreCompile")
   ├─ Execute SourceHashGenerator tool
   ├─ Scan source files in Loader + Loader.Contracts directories
   ├─ Compute SHA256 hash (truncated to 6 chars for readability)
   ├─ Write hash to intermediate file: source-hash-loader.txt
   └─ Expose hash as RcaLoaderSourceHash property

3. CoreCompile
   └─ Compile Rca.Loader.dll (NO assembly metadata generated in source code)

4. Build
   └─ Standard build output

5. RepackLoader (AfterTargets="Build")
   ├─ Create temp directory for Revit API references
   ├─ Copy RevitAPI.dll and RevitAPIUI.dll to temp dir
   ├─ Create internalize exclude list (Rca.Loader.Contracts)
   ├─ Run ILRepack:
   │  ├─ Input: Rca.Loader.dll + Rca.Loader.Contracts.dll
   │  ├─ Output: Rca.Loader.Merged.dll
   │  ├─ Options: /copyattrs /parallel /xmldocs
   │  ├─ /internalize:<exclude-file> (keep contracts public)
   │  └─ /lib:<temp-dir> (resolve Revit API)
   ├─ Replace original with merged
   ├─ Delete Rca.Loader.Contracts.dll
   └─ Clean up temp directory

6. BuildAttributeInjector (AfterTargets="RepackLoader")
   └─ Compile AttributeInjector tool (incremental - only if outdated)

7. InjectLoaderAttributes (AfterTargets="BuildAttributeInjector")
   ├─ Run AttributeInjector to inject assembly metadata POST-merge
   └─ Injected attributes:
      ├─ AssemblyMetadata("SourceHash", <computed-hash>)
      ├─ AssemblyMetadata("DeployFolder", <timestamp>)
      └─ AssemblyInformationalVersion("Hash: <hash>, Folder: <timestamp>")

8. DeployLoaderToTemp (AfterTargets="InjectLoaderAttributes")
   ├─ Copy merged Rca.Loader.dll to hot-reload deploy folder
   └─ Write version file: SourceHash-Loader-<hash>.txt
```

**Key Changes:**
- ✅ **NO source code generation** - Assembly metadata is NOT injected as C# source code before compilation
- ✅ **Post-merge injection** - AttributeInjector injects metadata directly into IL after ILRepack
- ✅ **Incremental tool compilation** - Tools only rebuild if their source files change
- ✅ **Unified naming** - Version files use PascalCase: `SourceHash-Loader-<hash>.txt`

### Runtime Build Pipeline

**File:** `src/Rca.Runtime/Rca.Runtime.csproj`

**Important:** Runtime has a **build-only dependency** on Loader to ensure correct build order, but references the **merged Rca.Loader.dll** as an assembly reference (not ProjectReference).

```xml
<!-- Reference the merged Rca.Loader.dll -->
<Reference Include="Rca.Loader">
  <HintPath>..\Rca.Loader\bin\$(Configuration)\net8.0-windows\Rca.Loader.dll</HintPath>
  <Private>True</Private>
</Reference>

<!-- Ensure Loader builds first -->
<ProjectReference Include="..\Rca.Loader\Rca.Loader.csproj">
  <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
</ProjectReference>
```

```
1. BuildSourceHashGenerator (BeforeTargets="GenerateRuntimeSourceHash")
   └─ Compile SourceHashGenerator tool (incremental - only if outdated)

2. GenerateRuntimeSourceHash (BeforeTargets="CoreCompile")
   ├─ Execute SourceHashGenerator tool
   ├─ Scan source files in all Runtime source roots
   ├─ Compute SHA256 hash (truncated to 6 chars for readability)
   ├─ Write hash to intermediate file: source-hash-runtime.txt
   └─ Expose hash as RcaSourceHash property

3. CoreCompile
   └─ Compile Rca.Runtime.dll (NO assembly metadata generated in source code)

4. Build
   └─ Standard build output

5. RepackRuntime (AfterTargets="Build")
   ├─ Run ILRepack:
   │  ├─ Input: Rca.Runtime.dll + Rca.Core.dll + Rca.UI.dll + 
   │  │         Rca.Network.dll + Rca.Contracts.dll
   │  ├─ Output: Rca.Runtime.dll (in-place)
   │  ├─ Options: /copyattrs /parallel /internalize /xmldocs
   │  └─ /lib:<TargetDir> (find dependencies)
   └─ All types internalized (no exclude list)

6. BuildAttributeInjector (AfterTargets="RepackRuntime")
   └─ Compile AttributeInjector tool (incremental - only if outdated)

7. InjectRuntimeAttributes (AfterTargets="BuildAttributeInjector")
   ├─ Run AttributeInjector to inject assembly metadata POST-merge
   └─ Injected attributes (same as Loader):
      ├─ AssemblyMetadata("SourceHash", <computed-hash>)
      ├─ AssemblyMetadata("DeployFolder", <timestamp>)
      └─ AssemblyInformationalVersion("Hash: <hash>, Folder: <timestamp>")

8. DeployRuntime (AfterTargets="InjectRuntimeAttributes")
   ├─ Copy merged Rca.Runtime.dll to hot-reload deploy folder
   ├─ Copy individual DLLs (for debugging/inspection)
   ├─ Copy IronPython assemblies
   ├─ Copy Rca.Logging.Contracts.dll
   ├─ Copy merged Rca.Loader.dll (needed for contract types)
   ├─ Copy Lib folder (Python stdlib)
   └─ Write version file: SourceHash-Runtime-<hash>.txt

9. ReloadRuntime (AfterTargets="DeployRuntime", Condition="HotReloadNotify==true")
   └─ Send RELOAD_RUNTIME command via named pipe to running Loader
```

## Build Constants and Naming Conventions

**File:** `src/Rca.Loader/Infrastructure/BuildConstants.cs`

All metadata keys and file patterns are centralized in `BuildConstants` to avoid magic strings:

```csharp
public static class BuildConstants
{
    // Assembly metadata keys (used by AttributeInjector and AssemblyStatusManager)
    public const string SourceHashMetadataKey = "SourceHash";
    public const string DeployFolderMetadataKey = "DeployFolder";

    // Version file patterns (used by AssemblyStatusManager)
    public const string LoaderHashFilePattern = "SourceHash-Loader-*.txt";
    public const string RuntimeHashFilePattern = "SourceHash-Runtime-*.txt";

    // Intermediate file names (used by MSBuild targets)
    public const string LoaderHashIntermediateFile = "source-hash-loader.txt";
    public const string RuntimeHashIntermediateFile = "source-hash-runtime.txt";
}
```

**Naming Convention:**
- ✅ **Deploy-time version files:** PascalCase with component name: `SourceHash-Loader-<hash>.txt`, `SourceHash-Runtime-<hash>.txt`
- ✅ **Intermediate files:** kebab-case: `source-hash-loader.txt`, `source-hash-runtime.txt`
- ✅ **Metadata keys:** PascalCase: `"SourceHash"`, `"DeployFolder"`

## Key Build Properties

### Global Properties (`Directory.Build.props`)

```xml
<RcaHotReloadTimestamp><!-- Shared timestamp for all projects --></RcaHotReloadTimestamp>
<GenerateAssemblyInformationalVersionAttribute>false</GenerateAssemblyInformationalVersionAttribute>
```

- **Why timestamp is shared:** Ensures Loader and Runtime deployed together have matching folder names
- **Why disable AssemblyInformationalVersion:** Prevents duplicate attributes - AttributeInjector injects them post-merge

### Common Properties (`build/Common.targets`)

```xml
<RcaRevitVersion>2026</RcaRevitVersion>
<RcaRevitLibsPath>$(SolutionDir)libs\Revit\$(RcaRevitVersion)</RcaRevitLibsPath>
<RcaAddinDir>$(RcaRevitAddinsDir)\Rca</RcaAddinDir>
<RcaRuntimeDeployRoot>$(LocalAppData)\RCA\Runtime</RcaRuntimeDeployRoot>
<RcaHotReloadDeployDir>$(LocalAppData)\RCA\Runtime\$(RcaHotReloadTimestamp)</RcaHotReloadDeployDir>
<BaseOutputPath>$(SolutionDir)bin\</BaseOutputPath>
```

- **RcaAddinDir:** Where Loader.dll is deployed for Revit to load
- **RcaHotReloadDeployDir:** Timestamped folder for hot-reload deployments
- **BaseOutputPath:** Unified output directory for all build artifacts

## ILRepack Configuration Details

### Loader ILRepack Command

```bash
ILRepack.exe 
  /out:"Rca.Loader.Merged.dll" 
  "Rca.Loader.dll" 
  "Rca.Loader.Contracts.dll" 
  /xmldocs 
  /copyattrs 
  /parallel 
  /internalize:"internalize-exclude.txt"  # Keep Rca.Loader.Contracts public
  /lib:"<temp-dir-with-RevitAPI>"         # Resolve Revit API references
```

**Internalize exclude file contents:**
```
Rca.Loader.Contracts
```

**Why exclude contracts from internalization:**
- Runtime needs to access `IRuntimePanelFactory`, `IRuntimePanelHost`, `SharedServiceRegistry`
- These types must remain public in merged assembly for cross-context access

### Runtime ILRepack Command

```bash
ILRepack.exe 
  /out:"Rca.Runtime.dll" 
  "Rca.Core.dll" 
  "Rca.UI.dll" 
  "Rca.Network.dll" 
  "Rca.Contracts.dll" 
  "Rca.Runtime.dll" 
  /lib:"<TargetDir>" 
  /copyattrs 
  /parallel 
  /internalize     # All types internalized
  /xmldocs
```

**Why full internalization:**
- Runtime types don't need to be accessed from outside
- Reduces potential conflicts and API surface

## AttributeInjector Post-Processing

**Purpose:** ILRepack's `/copyattrs` doesn't preserve custom assembly-level attributes properly. `AttributeInjector` uses **Mono.Cecil** to inject attributes after merge.

**Tool:** `src/Tools/AttributeInjector/Program.cs`

**Injected attributes:**
```csharp
[assembly: AssemblyMetadata("SourceHash", "<computed-hash>")]
[assembly: AssemblyMetadata("DeployFolder", "<timestamp>")]
[assembly: AssemblyInformationalVersion("Hash: <hash>, Folder: <timestamp>")]
```

**Why this matters:**
- **Hot-reload detection:** `AssemblyStatusManager` reads these attributes to detect outdated assemblies
- **Version tracking:** Visible in Windows Explorer properties (ProductVersion)
- **Deploy folder correlation:** Links assembly to its deploy folder

**New: No Source Code Generation**
- ❌ **OLD approach:** Generate `RcaLoaderAssemblyMetadata.cs` and `RcaAssemblyMetadata.cs` before compilation
- ✅ **NEW approach:** Inject all attributes post-merge using AttributeInjector only
- **Why:** Eliminates redundant source code generation and potential attribute conflicts

## Assembly Reference Strategy

### Why Runtime References Merged Loader.dll

**Problem:** Runtime needs access to contract types (`IRuntimePanelFactory`, etc) from `Rca.Loader.Contracts`, but that assembly is merged into `Rca.Loader.dll`.

**Solution:**
```xml
<!-- Runtime.csproj -->
<Reference Include="Rca.Loader">
  <HintPath>..\Rca.Loader\bin\$(Configuration)\net8.0-windows\Rca.Loader.dll</HintPath>
  <Private>True</Private>  <!-- Copy to output dir -->
</Reference>
```

**Why this works:**
1. Loader builds first and produces merged `Rca.Loader.dll`
2. Runtime compiles against merged assembly
3. Contract types are public (excluded from internalization)
4. Merged `Rca.Loader.dll` is copied to Runtime output dir
5. Runtime deploy copies it to hot-reload folder
6. `RuntimeLoadContext` loads it as non-collectible (shared between contexts)

### Non-Collectible Assembly List

**File:** `src/Rca.Loader/Infrastructure/AssemblyLoadConstants.cs`

```csharp
public static readonly string[] NonCollectibleAssemblies =
{
    "Rca.Loader.Contracts",  // Actually in merged Rca.Loader.dll
    "Rca.Logging.Contracts",
    "IronPython",
    "Microsoft.Scripting",
    // ...
};
```

**Why:** These assemblies must be loaded in default (non-collectible) context to:
- Share types across Loader and Runtime contexts (`SharedServiceRegistry`)
- Avoid IronPython collectible assembly issues
- Maintain type identity for contracts

## Deployment Structure

### Revit Addin Folder
```
%APPDATA%\Autodesk\Revit\Addins\2026\Rca\
└── Rca.Loader.dll  (merged, copied only on initial setup)
```

### Hot-Reload Deploy Folder
```
%LOCALAPPDATA%\RCA\Runtime\<timestamp>\
├── Rca.Runtime.dll                 (merged)
├── Rca.Loader.dll                  (merged, for contract types)
├── Rca.Core.dll                    (original, for debugging)
├── Rca.UI.dll                      (original, for debugging)
├── Rca.Network.dll                 (original, for debugging)
├── Rca.Contracts.dll               (original, for debugging)
├── Rca.Logging.Contracts.dll
├── IronPython*.dll
├── Microsoft.Scripting.dll
├── Microsoft.Dynamic.dll
├── SourceHash-Runtime-<hash>.txt   ← NEW: Unified PascalCase naming
├── SourceHash-Loader-<hash>.txt    ← NEW: Unified PascalCase naming
└── Lib\  (Python stdlib)
```

**Why keep original DLLs:**
- Debugging and inspection
- Source hash verification
- Fallback if merged assembly fails

## Build Order and Dependencies

### Critical Build Order

```
1. Tools (SourceHashGenerator, AttributeInjector)
   └─ Built incrementally (only when source changes)

2. Contracts (Rca.Contracts, Rca.Logging.Contracts, Rca.Loader.Contracts)
   └─ No dependencies, build first

3. Core Libraries (Rca.Core, Rca.UI, Rca.Network)
   └─ Depend on Contracts

4. Rca.Loader
   ├─ Depends on Rca.Loader.Contracts, Rca.Contracts, Rca.Logging.Contracts
   └─ Produces merged Rca.Loader.dll

5. Rca.Runtime
   ├─ Depends on Rca.Core, Rca.UI, Rca.Network, Rca.Contracts
   ├─ References merged Rca.Loader.dll
   └─ Produces merged Rca.Runtime.dll
```

### Enforcing Build Order

**Runtime.csproj:**
```xml
<ProjectReference Include="..\Rca.Loader\Rca.Loader.csproj">
  <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
  <SkipGetTargetFrameworkProperties>true</SkipGetTargetFrameworkProperties>
</ProjectReference>
```

- `ReferenceOutputAssembly=false` — Don't use project output, use assembly Reference instead
- Forces Loader to build before Runtime without creating circular dependency

## Incremental Compilation

### Tool Compilation Strategy

**Old approach:**
```xml
<!-- ALWAYS rebuild tools on every build -->
<Exec Command="dotnet build $(SourceHashProject)" />
```

**New approach:**
```xml
<!-- Only rebuild if tool source is newer than executable -->
<Target Name="BuildSourceHashGenerator" BeforeTargets="GenerateLoaderSourceHash">
  <PropertyGroup>
    <SourceHashProjectModTime>$([System.IO.File]::GetLastWriteTime('$(SourceHashProject)').Ticks)</SourceHashProjectModTime>
    <SourceHashExeModTime Condition="Exists('$(SourceHashExe)')">$([System.IO.File]::GetLastWriteTime('$(SourceHashExe)').Ticks)</SourceHashExeModTime>
    <SourceHashExeModTime Condition="!Exists('$(SourceHashExe)')">0</SourceHashExeModTime>
  </PropertyGroup>
  
  <MSBuild Projects="$(SourceHashProject)"
           Targets="Build"
           Properties="Configuration=$(Configuration)"
           Condition="'$(SourceHashProjectModTime)' &gt; '$(SourceHashExeModTime)'" />
</Target>
```

**Benefits:**
- ✅ Faster builds when tools haven't changed
- ✅ Uses `MSBuild` task instead of `Exec` for better integration
- ✅ Proper condition using Ticks (numeric comparison)

## Common Build Issues and Solutions

### Issue: "Could not load Rca.Loader.Contracts"
**Cause:** Runtime can't find contract types
**Solution:** Ensure `Rca.Loader.dll` is copied to Runtime output and deploy folder

### Issue: "IRuntimePanelFactory is inaccessible"
**Cause:** Contracts were internalized by ILRepack
**Solution:** Verify `internalize-exclude.txt` contains `Rca.Loader.Contracts`

### Issue: Assembly attributes missing after merge
**Cause:** ILRepack didn't preserve custom attributes
**Solution:** Verify `AttributeInjector` runs after ILRepack

### Issue: Build order violation
**Cause:** Runtime tries to build before Loader
**Solution:** Check ProjectReference with `ReferenceOutputAssembly=false` exists

### Issue: "Product Version" not showing in Windows Explorer
**Cause:** AssemblyInformationalVersion not injected properly
**Solution:** 
- ✅ Ensure `GenerateAssemblyInformationalVersionAttribute=false` in `.csproj`
- ✅ Verify `AttributeInjector` runs successfully
- ✅ Check injected attributes using `AttributeInjector inspect <assembly>`

## Strategic Considerations

### Pros of Current ILRepack Approach
✅ Simplified deployment (2 DLLs vs 7+)
✅ Fewer type identity issues
✅ Cleaner hot-reload tracking
✅ Single source-hash per unit
✅ **NEW:** No redundant source code generation
✅ **NEW:** Incremental tool compilation reduces build time

### Cons of Current ILRepack Approach
❌ Complex build configuration
❌ Requires post-processing (AttributeInjector)
❌ Build order dependencies more fragile
❌ Debugging merged assemblies harder

### Alternative: No ILRepack
**What would change if we removed ILRepack:**

1. **Deployment:** Would need to track 7+ DLLs instead of 2
2. **Build system:** Simpler - no merge step, no AttributeInjector
3. **Hot-reload:** More complex - track hashes for each DLL separately
4. **Type identity:** Potential issues with contracts loaded in multiple contexts
5. **Dependencies:** Simpler - standard ProjectReferences everywhere

**Code specific to ILRepack:**
- `RepackLoader` and `RepackRuntime` MSBuild targets
- `AttributeInjector` tool and invocations
- `internalize-exclude.txt` generation
- Special Reference configuration in Runtime.csproj
- Non-collectible assembly handling for merged Loader.dll

**Code that would remain (not ILRepack-specific):**
- `SharedServiceRegistry` - needed for cross-context communication
- `RuntimeLoadContext` - needed for collectible assembly loading
- `DockablePanelHost` proxy pattern - needed for UI hot-reload
- Source hash generation - would still need for change detection

### Recommendation
**Keep ILRepack for now** - benefits outweigh complexity, especially for hot-reload reliability. The UI hot-reload architecture (`SharedServiceRegistry` pattern) would be needed regardless of ILRepack usage.

## Future Improvements

1. ✅ ~~Cache AttributeInjector build~~ - DONE: Incremental compilation implemented
2. **Parallel ILRepack** - Loader and Runtime could merge in parallel
3. **Incremental merge check** - Skip merge if inputs unchanged
4. **Merge validation** - Automated tests to verify merged assemblies load correctly
5. **Single merged assembly** - Consider merging Loader + Runtime into one DLL (ambitious)

## References

- ILRepack documentation: https://github.com/gluck/il-repack
- Mono.Cecil: https://github.com/jbevain/cecil
- MSBuild Targets: https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-targets
