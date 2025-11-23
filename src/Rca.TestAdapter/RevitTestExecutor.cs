using System;
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Rca.TestAdapter;

/// <summary>
/// Executes NUnit tests in a Revit context using named pipes for communication.
/// </summary>
[ExtensionUri(Constants.ExecutorUri)]
public class RevitTestExecutor : ITestExecutor
{
  private bool cancelled;
  private readonly ITestRunCoordinator _testRunCoordinator;
  private readonly ISourceTestDiscoverer _sourceTestDiscoverer;

  /// <summary>
  /// Initializes a new instance of the <see cref="RevitTestExecutor"/> class.
  /// </summary>
  public RevitTestExecutor()
  {
    _testRunCoordinator = new RevitTestRunCoordinator(new RevitPipeClient(), TimeSpan.FromMinutes(2));
    _sourceTestDiscoverer = new NUnitSourceTestDiscoverer();
    cancelled = false;
  }

  /// <summary>
  /// Internal constructor used for testing to inject custom collaborators.
  /// </summary>
  internal RevitTestExecutor(ITestRunCoordinator testRunCoordinator, ISourceTestDiscoverer sourceTestDiscoverer)
  {
    _testRunCoordinator = testRunCoordinator ?? throw new ArgumentNullException(nameof(testRunCoordinator));
    _sourceTestDiscoverer = sourceTestDiscoverer ?? throw new ArgumentNullException(nameof(sourceTestDiscoverer));
    cancelled = false;
  }

  /// <summary>
  /// Runs the tests.
  /// </summary>
  public void RunTests(IEnumerable<TestCase>? tests, IRunContext? runContext, IFrameworkHandle? frameworkHandle)
  {
    if (tests == null || runContext == null || frameworkHandle == null)
    {
      return;
    }

    _testRunCoordinator.ExecuteTests(tests, runContext, frameworkHandle, () => cancelled);
  }

  /// <summary>
  /// Runs the specified tests.
  /// </summary>
  public void RunTests(IEnumerable<string>? sources, IRunContext? runContext, IFrameworkHandle? frameworkHandle)
  {
    if (sources == null || runContext == null || frameworkHandle == null)
    {
      return;
    }

    try
    {
      var discoveredTests = _sourceTestDiscoverer.DiscoverTests(sources, frameworkHandle);
      RunTests(discoveredTests, runContext, frameworkHandle);
    }
    catch (Exception ex)
    {
      frameworkHandle.SendMessage(TestMessageLevel.Error,
          $"RCA Test Adapter: Unexpected error: {ex.Message}");
    }
  }

  /// <summary>
  /// Cancels the current test run.
  /// </summary>
  public void Cancel()
  {
    cancelled = true;
  }
}
