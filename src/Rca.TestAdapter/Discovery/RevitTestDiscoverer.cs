using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Rca.TestAdapter;

/// <summary>
/// Discovers NUnit tests in assemblies that can be executed in the Revit context.
/// </summary>
[DefaultExecutorUri(Constants.ExecutorUri)]
[FileExtension(".dll")]
public class RevitTestDiscoverer : ITestDiscoverer
{
  private readonly ITestAssemblyLocator _assemblyLocator;
  private readonly ITestCasePublisher _testCasePublisher;

  /// <summary>
  /// Initializes a new instance of the <see cref="RevitTestDiscoverer"/> class with
  /// default RCA-specific services for locating test assemblies and publishing test cases.
  /// </summary>
  public RevitTestDiscoverer()
      : this(new RevitTestAssemblyLocator(), new RcaTestCasePublisher())
  {
  }

  /// <summary>
  /// Initializes a new instance of the <see cref="RevitTestDiscoverer"/> class.
  /// </summary>
  /// <param name="assemblyLocator">
  /// Service responsible for locating the runtime test assembly based on the current build layout.
  /// </param>
  /// <param name="testCasePublisher">
  /// Service responsible for enriching and publishing discovered test cases to the VSTest sink.
  /// </param>
  internal RevitTestDiscoverer(ITestAssemblyLocator assemblyLocator, ITestCasePublisher testCasePublisher)
  {
    ArgumentNullException.ThrowIfNull(assemblyLocator);
    ArgumentNullException.ThrowIfNull(testCasePublisher);
    _assemblyLocator = assemblyLocator;
    _testCasePublisher = testCasePublisher;
  }

  /// <summary>
  /// Discovers tests by scanning the latest runtime deployment folder and locating
  /// only the Rca.Integration.Revit.Tests.dll assembly. To satisfy VSTest rules,
  /// the TestCase.Source is set to a matching source from the provided list, while
  /// the real runtime path is stored in a custom TestProperty.
  /// </summary>
  /// <param name="sources">Ignored; discovery uses the latest runtime folder.</param>
  /// <param name="discoveryContext">The discovery context.</param>
  /// <param name="logger">The logger.</param>
  /// <param name="discoverySink">The discovery sink used to report discovered tests.</param>
  [SuppressMessage(
      "Microsoft.Maintainability",
      "CA1506:Avoid excessive class coupling",
      Justification = "Revit test discoverer is an orchestration point over VSTest abstractions and RCA discovery services; further decomposition would harm clarity.")]
  public void DiscoverTests(
      IEnumerable<string> sources,
      IDiscoveryContext discoveryContext,
      IMessageLogger logger,
      ITestCaseDiscoverySink discoverySink)
  {
    ArgumentNullException.ThrowIfNull(sources);
    ArgumentNullException.ThrowIfNull(logger);
    ArgumentNullException.ThrowIfNull(discoverySink);

    logger.SendMessage(TestMessageLevel.Informational, "RCA Test Adapter: Starting test discovery");

    try
    {
      if (!_assemblyLocator.TryLocateRuntimeTestAssembly(sources, logger, out var hostSource, out var testAssembly))
      {
        // Locator already logged why discovery was skipped or produced 0 tests.
        return;
      }

      try
      {
        var testCases = NUnitTestDiscoverer.FindTestsInAssembly(testAssembly);
        _testCasePublisher.PublishDiscoveredTests(testCases, hostSource, testAssembly, discoverySink);
        logger.SendMessage(TestMessageLevel.Informational, $"RCA Test Adapter: Discovered {testCases.Count} tests in {testAssembly}");
      }
      catch (Exception ex)
      {
        logger.SendMessage(TestMessageLevel.Error, $"RCA Test Adapter: Error discovering tests in {testAssembly}: {ex.Message}");
      }
    }
    catch (Exception ex)
    {
      logger.SendMessage(TestMessageLevel.Error, $"RCA Test Adapter: Discovery error: {ex.Message}");
    }

    logger.SendMessage(TestMessageLevel.Informational, "RCA Test Adapter: Test discovery completed");
  }
}


