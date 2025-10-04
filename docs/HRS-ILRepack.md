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
1. GenerateLoaderSourceHashAndMetadata (BeforeTargets="CoreCompile")
   ├─ Build SourceHashGenerator tool
   ├─ Generate source hash from Loader + Loader.Contracts sources
   ├─ Write hash to intermediate file
   └─ Generate RcaLoaderAssemblyMetadata.cs with attributes

2. CoreCompile
   └─ Compile Rca.Loader.dll (includes generated metadata)

3. Build
   └─ Standard build output

4. RepackLoader (AfterTargets="Build")
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
   ├─ Build AttributeInjector tool
   └─ Run AttributeInjector to inject metadata post-merge

5. DeployLoaderToTemp (AfterTargets="RepackLoader")
   ├─ Copy merged Rca.Loader.dll to hot-reload deploy folder
   └─ Write HashLoader - <hash>.txt version file
```

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
1. GenerateSourceHashAndMetadata (BeforeTargets="CoreCompile")
   ├─ Build SourceHashGenerator tool
   ├─ Generate source hash from all Runtime source roots
   └─ Generate RcaAssemblyMetadata.cs with attributes

2. CoreCompile
   └─ Compile Rca.Runtime.dll (includes generated metadata)

3. Build
   └─ Standard build output

4. RepackRuntime (AfterTargets="Build")
   ├─ Run ILRepack:
   │  ├─ Input: Rca.Runtime.dll + Rca.Core.dll + Rca.UI.dll + 
   │  │         Rca.Network.dll + Rca.Contracts.dll
   │  ├─ Output: Rca.Runtime.dll (in-place)
   │  ├─ Options: /copyattrs /parallel /internalize /xmldocs
   │  └─ /lib:<TargetDir> (find dependencies)
   ├─ Build AttributeInjector tool
   └─ Run AttributeInjector with Revit API path for resolution

5. DeployRuntime (AfterTargets="RepackRuntime")
   ├─ Copy merged Rca.Runtime.dll to hot-reload deploy folder
   ├─ Copy individual DLLs (for debugging/inspection)
   ├─ Copy IronPython assemblies
   ├─ Copy Rca.Logging.Contracts.dll
   ├─ Copy merged Rca.Loader.dll (needed for contract types)
   ├─ Copy Lib folder (Python stdlib)
   └─ Write HashRuntime - <hash>.txt version file

6. ReloadRuntime (AfterTargets="DeployRuntime", Condition="HotReloadNotify==true")
   └─ Send RELOAD_RUNTIME command via named pipe to running Loader
```

## Key Build Properties

### Global Properties (`Directory.Build.props`)

```xml
<RcaHotReloadTimestamp><!-- Shared timestamp for all projects --></RcaHotReloadTimestamp>
<GenerateAssemblyInformationalVersionAttribute>false</GenerateAssemblyInformationalVersionAttribute>
```

- **Why timestamp is shared:** Ensures Loader and Runtime deployed together have matching folder names
- **Why disable AssemblyInformationalVersion:** Prevents duplicate attributes - we inject them post-merge

### Common Properties (`build/Common.targets`)

```xml
<RcaRevitVersion>2026</RcaRevitVersion>
<RcaRevitLibsPath>$(SolutionDir)libs\Revit\$(RcaRevitVersion)</RcaRevitLibsPath>
<RcaAddinDir>$(RcaRevitAddinsDir)\Rca</RcaAddinDir>
<RcaRuntimeDeployRoot>$(LocalAppData)\RCA\Runtime</RcaRuntimeDeployRoot>
<RcaHotReloadDeployDir>$(LocalAppData)\RCA\Runtime\$(RcaHotReloadTimestamp)</RcaHotReloadDeployDir>
```

- **RcaAddinDir:** Where Loader.dll is deployed for Revit to load
- **RcaHotReloadDeployDir:** Timestamped folder for hot-reload deployments

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
├── Rca.Runtime.dll         (merged)
├── Rca.Loader.dll          (merged, for contract types)
├── Rca.Core.dll            (original, for debugging)
├── Rca.UI.dll              (original, for debugging)
├── Rca.Network.dll         (original, for debugging)
├── Rca.Contracts.dll       (original, for debugging)
├── Rca.Logging.Contracts.dll
├── IronPython*.dll
├── Microsoft.Scripting.dll
├── Microsoft.Dynamic.dll
├── HashRuntime - <hash>.txt
├── HashLoader - <hash>.txt
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
   └─ Built on-demand before use

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

## Strategic Considerations

### Pros of Current ILRepack Approach
✅ Simplified deployment (2 DLLs vs 7+)
✅ Fewer type identity issues
✅ Cleaner hot-reload tracking
✅ Single source-hash per unit

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

1. **Cache AttributeInjector build** - Don't rebuild every time
2. **Parallel ILRepack** - Loader and Runtime could merge in parallel
3. **Incremental merge check** - Skip merge if inputs unchanged
4. **Merge validation** - Automated tests to verify merged assemblies load correctly
5. **Single merged assembly** - Consider merging Loader + Runtime into one DLL (ambitious)

## References

- ILRepack documentation: https://github.com/gluck/il-repack
- Mono.Cecil: https://github.com/jbevain/cecil
- MSBuild Targets: https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-targets
