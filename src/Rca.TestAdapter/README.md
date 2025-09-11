# Rca.TestAdapter

A custom Visual Studio Test Adapter for running NUnit tests in Revit context via Named Pipes.

## Overview

This test adapter replaces `ricaun.RevitTest.TestAdapter` with a custom implementation that:

1. Discovers NUnit tests in test assemblies
2. Executes those tests in a running instance of Revit through a Named Pipe communication channel
3. Returns the results to Visual Studio Test Explorer

## Requirements

- Revit 2026 must be running with the RCA plugin loaded
- The RCA Pipe Server must be initialized (click the "Initialize" button in the RCA ribbon tab)

## How It Works

### Test Discovery

1. The test adapter scans test assemblies for classes with `[TestFixture]` attributes
2. It finds methods with `[Test]` attributes and registers them as test cases
3. Test cases are displayed in Visual Studio Test Explorer

### Test Execution

1. When you run tests, the adapter groups them by assembly
2. For each assembly, it sends the test requests through the Named Pipe
3. The tests execute in Revit's context via the `RevitTestExecutor` in the RCA plugin
4. Test results are returned through the pipe and displayed in the Test Explorer

## Using the Adapter

1. Reference the `Rca.TestAdapter` project or package in your test project
2. Make your test classes inherit from `Rca.Loader.Testing.UIApplicationTests`
3. Use the `uiapp` field to access the Revit UI Application

Example:

```csharp
using NUnit.Framework;
using Rca.Loader.Testing;

namespace MyTests
{
    [TestFixture]
    public class MyRevitTests : UIApplicationTests
    {
        [Test]
        public void Test_RevitContext_IsAvailable()
        {
            // The uiapp field is set by the test adapter
            Assert.IsNotNull(uiapp);
            Assert.IsNotNull(uiapp.Application);
        }
    }
}
```

## Debugging

If tests fail to execute:

1. Ensure Revit 2026 is running
2. Make sure the RCA plugin is loaded
3. Click the "Initialize" button in the RCA ribbon tab
4. Check the Output window for errors from the test adapter
5. Look for "RCA Test Adapter:" messages in the output

## Troubleshooting

### Common Issues

- **Pipe Connection Failure**: Revit isn't running or the RCA plugin isn't loaded
- **Test Discovery Error**: Check that tests use the NUnit attributes correctly
- **Test Execution Timeout**: Increase the timeout in the `Constants.cs` file
- **Test Results Missing**: Ensure the test class inherits from `UIApplicationTests`

## Implementation Details

The adapter consists of:

1. **RevitTestDiscoverer**: Finds NUnit tests in assemblies
2. **RevitTestExecutor**: Sends tests to Revit and processes results
3. **RevitPipeClient**: Handles Named Pipe communication
4. **RevitTestInitializer**: Ensures Revit is running and initialized

On the Revit side:

1. **RuntimeCommandHandler**: Receives the test commands
2. **RevitTestExecutor**: Executes tests in Revit context