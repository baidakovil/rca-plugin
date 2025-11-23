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
/// Default implementation of <see cref="ITestRunCoordinator"/> that uses
/// <see cref="RevitTestInitializer"/> and <see cref="RevitPipeClient"/> to execute tests
/// inside a running Revit instance.
/// </summary>
internal sealed class RevitTestRunCoordinator : ITestRunCoordinator
{
  private readonly RevitPipeClient _pipeClient;
  private readonly TimeSpan _defaultTimeout;

  public RevitTestRunCoordinator(RevitPipeClient pipeClient, TimeSpan defaultTimeout)
  {
    _pipeClient = pipeClient ?? throw new ArgumentNullException(nameof(pipeClient));
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
    if (tests is null)
      throw new ArgumentNullException(nameof(tests));
    if (runContext is null)
      throw new ArgumentNullException(nameof(runContext));
    if (frameworkHandle is null)
      throw new ArgumentNullException(nameof(frameworkHandle));
    if (isCancelled is null)
      throw new ArgumentNullException(nameof(isCancelled));

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


