# Analysis: ILRepack Impact on UI Hot-Reload Implementation

This document analyzes which parts of the UI hot-reload implementation are **necessary regardless of ILRepack** vs **added specifically because of ILRepack**.

## Executive Summary

**Short answer:** The core UI hot-reload architecture (SharedServiceRegistry + proxy pattern) would be needed **with or without ILRepack**. ILRepack adds build complexity but simplifies the runtime architecture.

## Components Analysis

### ✅ Required Regardless of ILRepack

These components are **fundamental** to hot-reloading UI across AssemblyLoadContexts and would be needed even with separate DLLs:

#### 1. SharedServiceRegistry
**File:** `src/Rca.Loader.Contracts/SharedServiceRegistry.cs`

**Why needed:**
- Loader and Runtime run in different AssemblyLoadContexts (non-collectible vs collectible)
- `ServiceContainer.Instance` is different in each context - they can't share state
- Need a cross-context communication mechanism for service registration

**Would be needed without ILRepack?** ✅ **YES**
- Even with separate `Rca.Loader.Contracts.dll`, it would be loaded in different contexts
- Static registry in non-collectible context is the cleanest cross-context pattern

#### 2. DockablePanelHost (Proxy Pattern)
**File:** `src/Rca.Loader/UI/DockablePanelHost.cs`

**Why needed:**
- Revit dockable panes must be registered at startup (can't register/unregister dynamically)
- UI content needs to survive Runtime unload/reload
- Need persistent container in Loader to host dynamic content from Runtime

**Would be needed without ILRepack?** ✅ **YES**
- Fundamental to any hot-reload UI architecture
- Revit API limitation, not ILRepack limitation

#### 3. IRuntimePanelFactory + IRuntimePanelHost Contracts
**Files:** 
- `src/Rca.Loader.Contracts/IRuntimePanelFactory.cs`
- `src/Rca.Loader.Contracts/IRuntimePanelHost.cs`

**Why needed:**
- Define cross-context communication protocols
- Abstract away concrete implementations
- Type identity must be preserved across contexts

**Would be needed without ILRepack?** ✅ **YES**
- These contracts would still live in a shared assembly (`Rca.Loader.Contracts.dll`)
- That assembly would still need to be loaded in default context as non-collectible

#### 4. RuntimePanelFactory
**File:** `src/Rca.Runtime/UI/RuntimePanelFactory.cs`

**Why needed:**
- Runtime must create UI with its own dependencies (PythonService, etc)
- Can't use reflection (no parameterless constructor)
- Factory pattern provides clean dependency resolution

**Would be needed without ILRepack?** ✅ **YES**
- Same pattern needed for any hot-reload architecture
- Factory pattern is the clean solution to "create object with dependencies from another context"

#### 5. RuntimeLoadContext Non-Collectible Assembly Handling
**File:** `src/Rca.Loader/Infrastructure/RuntimeLoadContext.cs`

```csharp
public static readonly string[] NonCollectibleAssemblies =
{
    "Rca.Loader.Contracts",  // Shared contract types
    "Rca.Logging.Contracts",  // Shared logging
    "IronPython",             // DLR requirements
    // ...
};
```

**Why needed:**
- Contract types must have same identity in both contexts
- IronPython requires default context loading
- Shared state must live in non-collectible context

**Would be needed without ILRepack?** ✅ **YES**
- Would be `Rca.Loader.Contracts.dll` instead of types in `Rca.Loader.dll`
- Same logic, just different assembly names

---

### ❌ Added Specifically Because of ILRepack

These components exist **only** because we're using ILRepack:

#### 1. Reference to Merged Rca.Loader.dll in Runtime
**File:** `src/Rca.Runtime/Rca.Runtime.csproj`

```xml
<Reference Include="Rca.Loader">
  <HintPath>..\Rca.Loader\bin\$(Configuration)\net8.0-windows\Rca.Loader.dll</HintPath>
  <Private>True</Private>
</Reference>
```

**Why needed:**
- Runtime needs access to contract types that are now in merged `Rca.Loader.dll`
- Can't use ProjectReference to `Rca.Loader.Contracts` (no longer exists as separate DLL)

**Without ILRepack:** Would be `<ProjectReference Include="..\Rca.Loader.Contracts\..."/>`

#### 2. Build-Only ProjectReference to Loader
**File:** `src/Rca.Runtime/Rca.Runtime.csproj`

```xml
<ProjectReference Include="..\Rca.Loader\Rca.Loader.csproj">
  <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
</ProjectReference>
```

**Why needed:**
- Enforce build order: Loader must build (and merge) before Runtime compiles
- Without this, Runtime might try to reference non-existent merged DLL

**Without ILRepack:** Standard ProjectReference would work

#### 3. Internalize Exclude List
**File:** Created dynamically in `Rca.Loader.csproj`

```xml
<WriteLinesToFile File="$(InternalizeExcludeFile)" 
                  Lines="Rca.Loader.Contracts" 
                  Overwrite="true" />
```

**Why needed:**
- Keep contract types public after merge
- By default, ILRepack makes all types internal

**Without ILRepack:** Not needed - contracts already public in separate DLL

#### 4. Copying Merged Rca.Loader.dll to Runtime Deploy Folder
**File:** `src/Rca.Runtime/Rca.Runtime.csproj`

```xml
<RuntimeDeployFiles Include="$(TargetDir)Rca.Loader.dll" />
```

**Why needed:**
- Runtime needs merged Loader.dll at runtime to resolve contract types
- Without it, `TypeLoadException` on Runtime load

**Without ILRepack:** Would copy `Rca.Loader.Contracts.dll` instead

#### 5. RepackLoader and RepackRuntime MSBuild Targets
**Files:**
- `src/Rca.Loader/Rca.Loader.csproj` - `RepackLoader` target
- `src/Rca.Runtime/Rca.Runtime.csproj` - `RepackRuntime` target

**Why needed:**
- Execute ILRepack merge process
- Complex orchestration of temp dirs, reference libs, etc

**Without ILRepack:** Not needed

#### 6. AttributeInjector Post-Processing
**File:** `src/Tools/AttributeInjector/Program.cs`

**Why needed:**
- ILRepack's `/copyattrs` doesn't preserve custom assembly attributes correctly
- Need Mono.Cecil to inject attributes after merge

**Without ILRepack:** Attributes would survive normal compilation

---

## Complexity Scorecard

### Build System Complexity

| Aspect | With ILRepack | Without ILRepack |
|--------|---------------|------------------|
| **MSBuild targets** | Complex (merge + inject) | Simple (standard build) |
| **Build order** | Fragile (needs careful ordering) | Standard dependencies |
| **Tool dependencies** | ILRepack + AttributeInjector | None |
| **csproj complexity** | High (custom targets, temp dirs) | Low (standard references) |
| **Build time** | Slower (merge step) | Faster |
| **Debugging builds** | Harder (need source-link merged) | Easier |

### Runtime Complexity

| Aspect | With ILRepack | Without ILRepack |
|--------|---------------|------------------|
| **Assembly count** | 2 main DLLs | 7+ DLLs |
| **Type identity** | Simpler (fewer assemblies) | More complex |
| **Deploy folder** | Cleaner | More files to track |
| **Hot-reload tracking** | 2 hashes | 7+ hashes |
| **Load context logic** | Same | Same |

### Code Complexity (UI Hot-Reload)

| Component | With ILRepack | Without ILRepack |
|-----------|---------------|------------------|
| **SharedServiceRegistry** | Same | Same |
| **DockablePanelHost** | Same | Same |
| **RuntimePanelFactory** | Same | Same |
| **Contract interfaces** | Same | Same |
| **RuntimeLoadContext** | Same logic, different names | Same logic, different names |

## Recommendations

### If Removing ILRepack

**Build changes needed:**
1. ✅ Remove `RepackLoader` and `RepackRuntime` targets
2. ✅ Remove `AttributeInjector` tool (no longer needed)
3. ✅ Change Runtime reference to Loader.Contracts back to ProjectReference
4. ✅ Remove build-only Loader ProjectReference from Runtime
5. ✅ Update deploy targets to copy all individual DLLs

**Runtime changes needed:**
1. ✅ Update `AssemblyLoadConstants.NonCollectibleAssemblies` to use separate DLL names
2. ✅ Deploy `Rca.Loader.Contracts.dll` instead of merged `Rca.Loader.dll`
3. ✅ Update hot-reload system to track more source hashes

**Code that stays the same:**
1. ✅ `SharedServiceRegistry` - still needed for cross-context communication
2. ✅ `DockablePanelHost` - still needed for UI hot-reload
3. ✅ `RuntimePanelFactory` - still needed for dependency injection
4. ✅ All contract interfaces
5. ✅ RuntimeLoadContext assembly resolution logic (just different names)

### Cost-Benefit Analysis

**Keeping ILRepack:**
- ✅ Simpler deployment (fewer files)
- ✅ Cleaner hot-reload (fewer hashes to track)
- ✅ Reduced type identity issues
- ❌ Complex build system
- ❌ Harder to debug
- ❌ Fragile build order

**Removing ILRepack:**
- ✅ Simpler build system
- ✅ Easier debugging
- ✅ More maintainable
- ❌ More DLLs to manage
- ❌ More complex hot-reload tracking
- ❌ Potential type identity issues

### Final Verdict

**Keep ILRepack** if:
- You value clean deployment over build simplicity
- Hot-reload reliability is critical
- You're comfortable with MSBuild complexity

**Remove ILRepack** if:
- Build maintainability is top priority
- You need easier debugging
- Team is uncomfortable with advanced MSBuild
- You're okay managing more DLLs

**Personal recommendation:** Keep ILRepack for now. The UI hot-reload architecture is clean and ILRepack-agnostic. The build complexity is well-documented and stable. The benefits (2 DLLs vs 7+) are worth it for a hot-reload system.

## Migration Path (If Removing ILRepack)

### Step 1: Update Project References
```xml
<!-- Runtime.csproj - BEFORE -->
<Reference Include="Rca.Loader">
  <HintPath>..\Rca.Loader\bin\$(Configuration)\net8.0-windows\Rca.Loader.dll</HintPath>
</Reference>

<!-- Runtime.csproj - AFTER -->
<ProjectReference Include="..\Rca.Loader.Contracts\Rca.Loader.Contracts.csproj" />
```

### Step 2: Remove Merge Targets
Delete `RepackLoader` and `RepackRuntime` targets from csproj files.

### Step 3: Update Deploy Targets
```xml
<!-- Deploy all DLLs individually -->
<RuntimeDeployFiles Include="$(TargetDir)Rca.Runtime.dll" />
<RuntimeDeployFiles Include="$(TargetDir)Rca.Core.dll" />
<RuntimeDeployFiles Include="$(TargetDir)Rca.UI.dll" />
<RuntimeDeployFiles Include="$(TargetDir)Rca.Network.dll" />
<RuntimeDeployFiles Include="$(TargetDir)Rca.Contracts.dll" />
<RuntimeDeployFiles Include="$(TargetDir)Rca.Loader.Contracts.dll" />
<!-- etc -->
```

### Step 4: Update AssemblyLoadConstants
```csharp
// Change this:
"Rca.Loader.Contracts",  // In merged Rca.Loader.dll

// To this:
"Rca.Loader.Contracts",  // Separate DLL
```

### Step 5: Update Source Hash Generation
Generate separate hashes for each project, or create combined hash strategy.

### Step 6: Test
- Build clean
- Deploy
- Test hot-reload
- Verify type identity across contexts

---

**Conclusion:** The UI hot-reload architecture is **fundamentally sound** and **not dependent on ILRepack**. ILRepack is a deployment optimization that trades build complexity for runtime simplicity. The core pattern (`SharedServiceRegistry` + proxy) would be identical with or without it.
