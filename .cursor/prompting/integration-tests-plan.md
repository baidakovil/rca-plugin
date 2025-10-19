# Integration tests modernization plan for Revit Add-in (RCA)

Goal
- Remove file locks on test assemblies and enable fast reload of integration tests without restarting Revit.
- Discover and execute tests only from the latest runtime deployment folder under `%LOCALAPPDATA%/RCA/Runtime/<timestamp>`.

Scope
- Only `tests/Rca.Integration.Revit.Tests` and `src/Rca.TestAdapter` + `src/Rca.Loader` test-execution path.
- Other unit test projects remain unchanged.

Decisions confirmed
- Copy integration test artifacts into the root of the latest runtime folder: `%LOCALAPPDATA%/RCA/Runtime/<timestamp>` (no Tests subfolder).
- Discover and run only `Rca.Integration.Revit.Tests.dll` (no wildcard for `*.Tests.dll`).
- Do not copy `.runtimeconfig.json` for tests; Runtime’s config is sufficient.
- There is only one `Runtime/<timestamp>` per Revit version; always choose the latest by lexicographical name.
- No known native deps to special-case; a prior error about collectible vs non-collectible may be resolved by the new ALC-based execution.

Design
- Dedicated collectible `AssemblyLoadContext` for tests (`TestLoadContext`) using `AssemblyDependencyResolver`.
- Normal test run: load tests into fresh `TestLoadContext`, execute, then `Unload` + `GC`; do not unload Runtime.
- `RELOAD_RUNTIME` command: force-unload `TestLoadContext`, unload `RuntimeLoadContext`, then load Runtime only.
- Post-build: copy only `Rca.Integration.Revit.Tests.*` (`dll`/`pdb`/`xml`/`deps.json` if present) to latest runtime folder to avoid collisions with runtime assemblies.

Tasks
1) Post-build copy
- File: `tests/Rca.Integration.Revit.Tests/Rca.Integration.Revit.Tests.csproj`
- Add `AfterBuild` target to:
  - Resolve `%LOCALAPPDATA%/RCA/Runtime` and select the latest subfolder by ordinal sort.
  - Copy `Rca.Integration.Revit.Tests.*` to that folder (overwrite). Fail with clear error if no runtime folder exists.

2) Discovery from latest runtime folder
- File: `src/Rca.TestAdapter/RevitTestDiscoverer.cs`
- Ignore provided sources; resolve latest runtime folder and locate `Rca.Integration.Revit.Tests.dll` there.
- Discover NUnit tests via `NUnitTestDiscoverer.FindTestsInAssembly()` and report.
- Log resolved folder path, counts, and friendly errors if not found.

3) Execute tests in isolated, collectible ALC
- File: `src/Rca.Loader/Testing/RevitTestExecutor.cs`
- Implement `TestLoadContext` with `AssemblyDependencyResolver` and fallback to default ALC for `RevitAPI`.
- Per run: create fresh `TestLoadContext`, load test assembly from latest runtime folder using `LoadFromAssemblyPath`, execute, then `Unload` + `GC` twice.
- Maintain static `WeakReference` to active `TestLoadContext` and provide `ForceUnloadActive()` API.

4) Forced unload on `RELOAD_RUNTIME`
- File: `src/Rca.Loader/Infrastructure/RuntimeCommandHandler.cs`
- Before reloading runtime, call `RevitTestExecutor.ForceUnloadActiveTestLoadContext()`.
- Proceed to unload/reload runtime as today.

Validation workflow
- Clean VS test caches; build Runtime (to create runtime folder); build integration tests (post-build copies artifacts).
- Open Revit with RCA, trigger discovery; expect adapter logs to show latest folder and discovered tests.
- Run tests; verify ALC unload sequence and no locks in tests/bin.

Risks and mitigations
- Assembly identity collisions avoided by copying only test artifacts; dependencies come from runtime folder.
- Potential ALC unload issues mitigated by `WeakReference` and forced unload path on `RELOAD_RUNTIME`.
