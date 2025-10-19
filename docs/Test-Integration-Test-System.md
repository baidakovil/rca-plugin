# Integration Test System

This document describes how integration tests are discovered, executed, and reloaded in the RCA project.

Audience: contributors maintaining the test infrastructure and developers authoring integration tests.

## Goals

- Run NUnit tests inside a live Revit 2026 process.
- Avoid file locks so multiple builds/runs can happen back-to-back.
- Keep a single copy of `Rca.*` assemblies loaded (no type identity conflicts across AssemblyLoadContexts).
- Make discovery and execution stable inside Visual Studio Test Explorer.

## Components

- `Rca.TestAdapter` (VS Test Adapter)
  - `RevitTestDiscoverer` — finds tests for VS Test Explorer.
  - `RevitTestExecutor` — sends test requests to Revit via Named Pipe and publishes results.
  - `RevitPipeClient` — pipe transport implementation.
  - `RevitTestInitializer` — checks Revit is running and the RCA pipe server is responsive.
  - `AdapterProperties` — defines `RuntimeAssemblyPath` custom property used to carry the actual DLL path for execution.

- Loader/Runtime (in Revit process)
  - `RuntimeCommandHandler` — receives pipe commands `STATUS`, `RUN_TESTS`, etc.
  - `Rca.Loader.Testing.RevitTestExecutor` — loads test assembly into a collectible `AssemblyLoadContext` and runs tests.

- MSBuild (test project)
  - `tests/Rca.Integration.Revit.Tests/Rca.Integration.Revit.Tests.csproj` — post-build creates `%LOCALAPPDATA%\RCA\Test\<yyyyMMdd_HHmmss>` and copies test artifacts there.
  - Artifacts include: `Rca.Integration.Revit.Tests.*`, `nunit.framework*.dll`, `FluentAssertions*.dll`.

- Constants
  - `LoaderConstants.TestDeployRoot` — `%LOCALAPPDATA%\RCA\Test` root for test deploys.
  - Timestamped subfolders: `<yyyyMMdd_HHmmss>` — one-per-build to avoid file locking.

## Discovery Pipeline

1. Test project builds and copies artifacts to `%LOCALAPPDATA%\RCA\Test\<timestamp>`.
2. `RevitTestDiscoverer`:
   - Receives the list of sources (VS Test Explorer provides `bin\...\Rca.Integration.Revit.Tests.dll`).
   - Picks a host source from that list (needed for VS rules).
   - Locates the latest test folder under `TestDeployRoot` and finds `Rca.Integration.Revit.Tests.dll` there.
   - Discovers tests in that DLL using `NUnitTestDiscoverer` (inside the adapter process, isolated ALC to avoid file locks).
   - Emits `TestCase` where:
     - `Source` is set to the host source (so VS keeps the case).
     - `RuntimeAssemblyPath` is set to the full path in the test deploy folder (real execution path).
     - Adds trait `Adapter=RCA` for quick filtering.

Rationale: VS requires `TestCase.Source` to be among the provided sources. We keep that, but carry the real execution path in a custom property the executor can use.

## Execution Pipeline

1. `RevitTestExecutor` groups `TestCase`s by `RuntimeAssemblyPath` and sends a `RUN_TESTS` command over the pipe (payload: assembly path + FQNs).
2. In Revit, `RuntimeCommandHandler` deserializes the payload and calls `Rca.Loader.Testing.RevitTestExecutor` to execute.
3. `Rca.Loader.Testing.RevitTestExecutor`:
   - Creates a collectible `AssemblyLoadContext` (Test ALC) bound to the test DLL path.
   - Resolution order in `TestLoadContext`:
     - For `Rca.*` assemblies: always reuse an already loaded assembly (from Runtime context) to avoid duplicates. If not found, delegate to default context (never load `Rca.*` from the test folder).
     - For other managed dependencies: try `AssemblyDependencyResolver` (deps.json), then probe in the test folder (e.g., `nunit.framework.dll`, `FluentAssertions.dll`), then default context.
     - For native libraries: similar approach (`AssemblyDependencyResolver` then probe).
   - Loads assembly, resolves test class/method, runs `[SetUp]`, invokes test, collects result.
   - Unloads the Test ALC at the end to drop file locks and allow repeated runs.

## Avoiding File Locks

- Tests are copied into a new timestamped directory on every build.
- In Revit process, test DLLs are loaded via a collectible ALC and then unloaded.
- `Rca.*` assemblies are never loaded from the test folder; the already loaded Runtime copy is reused, preventing type identity conflicts and keeping these assemblies unlocked.

## Hot Reload / Runtime Reload

- Loader/runtime reload logic is independent of the test deploy path. Tests continue to be discovered from the latest `%LOCALAPPDATA%\RCA\Test\<timestamp>` folder.
- On runtime reload, test execution still reuses `Rca.*` from the active Runtime context.

## Environment and Traits

- Revit 2026 must be running with the RCA plugin and pipe server initialized.
- Optional environment flag used by some tests: `RCA_ENABLE_REVIT_TESTS=1`.
- In Test Explorer, filter by trait `Adapter=RCA` to show tests discovered by this adapter.

## Failure Modes & Diagnostics

- Pipe connection issues: ensure Revit is running and the plugin is initialized.
- Missing `nunit.framework.dll` / `FluentAssertions.dll`: verify post-build copy into the test deploy folder.
- Assembly identity conflicts: adapter logs (and Revit logs) indicate reuse of `Rca.*` from runtime; if warnings show attempts to load `Rca.*` from test folder, check for stray copies.
- Discovery emits messages prefixed with `RCA Test Adapter:` in the VS Output (Tests) pane.

## Authoring Tests

- Use NUnit `[TestFixture]` and `[Test]`.
- Inherit from `UIApplicationTestsBase` (or similar) for access to the `uiapp` context.
- Use `FluentAssertions` for assertions if desired; the csproj already copies it to the test deploy folder.

## Summary of Key Paths

- Test deploy root: `%LOCALAPPDATA%\RCA\Test` (see `LoaderConstants.TestDeployRoot`).
- Latest test folder: `%LOCALAPPDATA%\RCA\Test\<yyyyMMdd_HHmmss>`.
- Runtime deploy root (separate): `%LOCALAPPDATA%\RCA\Runtime`.

