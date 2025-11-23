# Rca.TestAdapter

A custom Visual Studio Test Adapter for running NUnit tests in Revit context via Named Pipes.

## Overview

This test adapter replaces `ricaun.RevitTest.TestAdapter` with a custom implementation that:

1. Discovers NUnit tests in test assemblies
2. Executes those tests in a running instance of Revit through a Named Pipe communication channel
3. Returns the results to Visual Studio Test Explorer

See also: `docs/Test-Integration-Test-System.md` for the end-to-end integration testing architecture.

## Requirements

- Revit 2026 must be running with the RCA plugin loaded
- The RCA Pipe Server must be initialized (click the "Initialize" button in the RCA ribbon tab)

## How It Works

### Test Discovery

1. The test adapter scans test assemblies for classes with `[TestFixture]` attributes
2. It finds methods with `[Test]` attributes and registers them as test cases
3. Test cases are displayed in Visual Studio Test Explorer

Adapter specifics:

- Tests are physically copied during test project build to a timestamped folder under `%LOCALAPPDATA%\RCA\Test\<yyyyMMdd_HHmmss>`.
- `RevitTestDiscoverer` locates the latest test folder and discovers tests from the `Rca.Integration.Revit.Tests.dll` there.
- For VS compatibility, `TestCase.Source` remains the original `bin\...` path, while the real execution path is stored in a custom property (`RuntimeAssemblyPath`).

### Test Execution

1. When you run tests, the adapter groups them by the real execution path (`RuntimeAssemblyPath`).
2. For each group, it sends the test requests through the Named Pipe (command `RUN_TESTS`).
3. The tests execute in Revit's context via the Loader-side `RevitTestExecutor`.
4. Test results are returned through the pipe and displayed in the Test Explorer.

### Dependency Resolution

- The Loader-side `RevitTestExecutor` uses a collectible `AssemblyLoadContext` with the following order:
  - Reuse already loaded `Rca.*` assemblies from Runtime (avoid duplicates).
  - For third-party libs (e.g., NUnit, FluentAssertions), load from the test folder.
  - Fallback to default context for shared/native dependencies.
- Test ALC is unloaded after each run to avoid file locks.

## Using the Adapter

1. Reference the `Rca.TestAdapter` project or package in your test project
2. Ensure your test project post-build copies artifacts to `%LOCALAPPDATA%\RCA\Test\<timestamp>` (already configured)
3. Make your test classes inherit from `UIApplicationTestsBase`
4. Use the `uiapp` field to access the Revit UI Application

Example:

```csharp
using NUnit.Framework;
using Rca.Integration.Revit.Tests.Infrastructure;

namespace MyTests
{
    [TestFixture]
    public class MyRevitTests : UIApplicationTestsBase
    {
        [Test]
        public void Test_RevitContext_IsAvailable()
        {
            Assert.That(uiapp, Is.Not.Null);
            Assert.That(uiapp!.Application, Is.Not.Null);
        }
    }
}
```

## Debugging

If tests fail to execute:

1. Ensure Revit 2026 is running
2. Make sure the RCA plugin is loaded and the pipe server is initialized
3. Check the Output window for messages starting with `RCA Test Adapter:`
4. See `docs/Test-Integration-Test-System.md` for resolution rules and troubleshooting.

## Troubleshooting

- Pipe Connection Failure: Revit isn't running or the RCA plugin isn't loaded
- Test Discovery Error: Check NUnit attributes and that the test deploy folder exists
- Missing Dependencies: Verify `nunit.framework.dll` and `FluentAssertions.dll` were copied to the deploy folder
- No Results: Ensure the test classes inherit from the proper base and `RCA_ENABLE_REVIT_TESTS=1` if required by tests

## Implementation Details

- `RevitTestDiscoverer` — discovery orchestration and `RuntimeAssemblyPath` assignment
- `RevitTestAssemblyLocator` — locates the latest `%LOCALAPPDATA%\RCA\Test\<timestamp>` folder and resolves the runtime `Rca.Integration.Revit.Tests.dll` path
- `RcaTestCasePublisher` — applies RCA-specific metadata to discovered `TestCase` instances and publishes them to VSTest
- `NUnitTestDiscoverer` / `NUnitTestCaseFactory` — reflection-based NUnit discovery in a collectible `AssemblyLoadContext`
- `RevitTestExecutor` — VSTest `ITestExecutor` implementation; delegates execution to coordinator/transport
- `RevitTestRunCoordinator` — groups tests by runtime assembly, coordinates execution via `RevitPipeClient`, and maps pipe results to VSTest `TestResult`
- `NUnitSourceTestDiscoverer` — discovers tests directly from source assemblies for the `RunTests(IEnumerable<string> sources, ...)` path
- `RevitPipeClient` — high-level pipe client used by the coordinator
- `PipeTestExecutionTransport` — low-level pipe transport that sends `RUN_TESTS` commands and deserializes `RevitTestResult` payloads
- `NamedPipeJsonClient` — shared helper for JSON-over-named-pipe request/response handling
- `RevitTestInitializer` — environment checks (Revit process + RCA pipe server availability); when Revit is not available, integration tests are reported as *Skipped* rather than causing a hard failure

On the Revit side:

- `RuntimeCommandHandler` — receives commands
- `Rca.Loader.Testing.RevitTestExecutor` — execution in collectible ALC
