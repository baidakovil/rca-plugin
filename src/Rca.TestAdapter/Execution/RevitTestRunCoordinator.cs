using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Rca.TestAdapter;

/// <summary>
/// Coordinates execution of Revit integration tests for a given set of test cases.
/// </summary>
internal interface ITestRunCoordinator
{
  /// <summary>
  /// Executes the specified tests using the provided VSTest abstractions.
  /// </summary>
  /// <param name="tests">The tests to execute.</param>
  /// <param name="runContext">The run context.</param>
  /// <param name="frameworkHandle">The framework handle.</param>
  /// <param name="isCancelled">
  /// Delegate that returns <see langword="true"/> when the run has been cancelled.
  /// </param>
  void ExecuteTests(
      IEnumerable<TestCase> tests,
      IRunContext runContext,
      IFrameworkHandle frameworkHandle,
      Func<bool> isCancelled);
}

/// <summary>
/// Default implementation of <see cref="ITestRunCoordinator"/> that coordinates
/// Revit integration test execution. This class encapsulates the high-level flow:
/// checking Revit availability, grouping tests by runtime assembly, dispatching
/// execution via <see cref="RevitPipeClient"/>, and translating raw pipe results
/// into VSTest <see cref="TestResult"/> instances.
/// <remarks>
/// WHY: We keep this orchestration logic in a single place so that
/// <see cref="RevitTestExecutor"/> remains a thin adapter over VSTest APIs. Low-level
/// concerns such as pipe transport and JSON serialization are delegated to
/// <see cref="RevitPipeClient"/> and <see cref="PipeTestExecutionTransport"/>, which
/// makes the coordination code easier to reason about and maintain.
/// </remarks>
/// </summary>
internal sealed class RevitTestRunCoordinator : ITestRunCoordinator
{
  private readonly RevitPipeClient _pipeClient;
  private readonly TimeSpan _defaultTimeout;

  public RevitTestRunCoordinator(RevitPipeClient pipeClient, TimeSpan defaultTimeout)
  {
    ArgumentNullException.ThrowIfNull(pipeClient);
    _pipeClient = pipeClient;
    _defaultTimeout = defaultTimeout;
  }

  [SuppressMessage(
      "Microsoft.Maintainability",
      "CA1506:Avoid excessive class coupling",
      Justification = "Revit test run coordinator is an orchestration point over VSTest abstractions and RCA execution services; low-level details are already delegated to dedicated components.")]
  public void ExecuteTests(
      IEnumerable<TestCase> tests,
      IRunContext runContext,
      IFrameworkHandle frameworkHandle,
      Func<bool> isCancelled)
  {
    ArgumentNullException.ThrowIfNull(tests);
    ArgumentNullException.ThrowIfNull(runContext);
    ArgumentNullException.ThrowIfNull(frameworkHandle);
    ArgumentNullException.ThrowIfNull(isCancelled);

    frameworkHandle.SendMessage(TestMessageLevel.Informational, "RCA Test Adapter: Starting test execution");

    if (!EnsureRevitInitializedOrSkip(tests, frameworkHandle))
    {
      return;
    }

    var testsByGroup = GroupTestsByAssembly(tests);

    foreach (var group in testsByGroup)
    {
      if (isCancelled())
      {
        break;
      }

      ExecuteTestGroup(group.Key, group.ToList(), frameworkHandle);
    }

    frameworkHandle.SendMessage(TestMessageLevel.Informational, "RCA Test Adapter: Test execution completed");
  }

  private static bool EnsureRevitInitializedOrSkip(IEnumerable<TestCase> tests, IFrameworkHandle frameworkHandle)
  {
    // Ensure Revit is initialized. When Revit is not running we treat this as a
    // skipped scenario rather than a hard error so that CI can succeed without Revit.
    if (RevitTestInitializer.EnsureRevitIsInitialized())
    {
      return true;
    }

    frameworkHandle.SendMessage(
        TestMessageLevel.Warning,
        "RCA Test Adapter: Revit is not initialized. Revit integration tests will be skipped. " +
        "Start Autodesk Revit with the RCA plugin loaded to enable these tests.");

    foreach (var testCase in tests)
    {
      var skippedResult = new TestResult(testCase)
      {
        Outcome = TestOutcome.Skipped,
        ErrorMessage = "Revit is not running or RCA pipe server is not available. Test skipped.",
      };

      frameworkHandle.RecordResult(skippedResult);
    }

    frameworkHandle.SendMessage(TestMessageLevel.Informational, "RCA Test Adapter: Test execution skipped (Revit not running)");
    return false;
  }

  private static IEnumerable<IGrouping<string, TestCase>> GroupTestsByAssembly(IEnumerable<TestCase> tests)
  {
    // Group tests by runtime assembly path if available, otherwise by source.
    return tests.GroupBy(GetAssemblyKeyForTest);
  }

  private static string GetAssemblyKeyForTest(TestCase test)
  {
    return test.GetPropertyValue(AdapterProperties.RuntimeAssemblyPath, defaultValue: test.Source);
  }

  /// <summary>
  /// Executes a homogeneous group of tests that share the same runtime assembly path.
  /// </summary>
  /// <param name="assemblyPath">The runtime assembly path used for execution.</param>
  /// <param name="sourceTests">The tests mapped to this assembly path.</param>
  /// <param name="frameworkHandle">VSTest framework handle for logging and recording results.</param>
  [SuppressMessage(
      "Microsoft.Maintainability",
      "CA1506:Avoid excessive class coupling",
      Justification = "ExecuteTestGroup is an orchestration helper that maps RCA pipe results to VSTest TestResult instances; lower-level concerns are already delegated to RevitPipeClient and transport.")]
  private void ExecuteTestGroup(string assemblyPath, List<TestCase> sourceTests, IFrameworkHandle frameworkHandle)
  {
    try
    {
      frameworkHandle.SendMessage(
          TestMessageLevel.Informational,
          $"RCA Test Adapter: Executing {sourceTests.Count} tests from {assemblyPath}");

      var results = _pipeClient.ExecuteTests(assemblyPath, sourceTests, _defaultTimeout);

      foreach (var result in results)
      {
        var testCase = sourceTests.FirstOrDefault(t =>
            t.FullyQualifiedName == result.FullyQualifiedName);

        if (testCase != null)
        {
          var testResult = new TestResult(testCase)
          {
            Outcome = ConvertTestOutcome(result.Outcome),
            ErrorMessage = result.ErrorMessage,
            ErrorStackTrace = result.ErrorStackTrace,
            DisplayName = result.DisplayName,
            Duration = TimeSpan.FromMilliseconds(result.DurationInMilliseconds),
            StartTime = DateTimeOffset.FromUnixTimeMilliseconds(result.StartTimeUnixMs),
            EndTime = DateTimeOffset.FromUnixTimeMilliseconds(result.EndTimeUnixMs),
          };

          foreach (var message in result.Messages)
          {
            frameworkHandle.SendMessage(ConvertMessageLevel(message.Level), message.Text);
          }

          frameworkHandle.RecordResult(testResult);
        }
      }
    }
    catch (Exception ex)
    {
      frameworkHandle.SendMessage(
          TestMessageLevel.Error,
          $"RCA Test Adapter: Error executing tests: {ex.Message}");
      frameworkHandle.SendMessage(
          TestMessageLevel.Informational,
          $"RCA Test Adapter: Exception details: {ex}");
    }
  }

  private static TestOutcome ConvertTestOutcome(string outcome)
  {
    return outcome switch
    {
      "Passed" => TestOutcome.Passed,
      "Failed" => TestOutcome.Failed,
      "Skipped" => TestOutcome.Skipped,
      "NotFound" => TestOutcome.NotFound,
      _ => TestOutcome.None
    };
  }

  private static TestMessageLevel ConvertMessageLevel(string level)
  {
    return level switch
    {
      "Error" => TestMessageLevel.Error,
      "Warning" => TestMessageLevel.Warning,
      _ => TestMessageLevel.Informational
    };
  }
}


