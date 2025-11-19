using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
  public void DiscoverTests(
      IEnumerable<string> sources,
      IDiscoveryContext discoveryContext,
      IMessageLogger logger,
      ITestCaseDiscoverySink discoverySink)
  {
    if (sources is null)
      throw new ArgumentNullException(nameof(sources));
    if (logger is null)
      throw new ArgumentNullException(nameof(logger));
    if (discoverySink is null)
      throw new ArgumentNullException(nameof(discoverySink));

    logger.SendMessage(TestMessageLevel.Informational, "RCA Test Adapter: Starting test discovery");

    try
    {
      // Choose a host source that VSTest recognizes (must be one of the provided sources)
      var hostSource = sources.FirstOrDefault(s =>
          string.Equals(Path.GetFileName(s), "Rca.Integration.Revit.Tests.dll", StringComparison.OrdinalIgnoreCase));
      if (string.IsNullOrEmpty(hostSource))
      {
        logger.SendMessage(TestMessageLevel.Warning, "RCA Test Adapter: No matching host source provided. Discovery skipped.");
        logger.SendMessage(TestMessageLevel.Informational, "RCA Test Adapter: Test discovery completed (0 tests)");
        return;
      }

      var testRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RCA", "Test");
      var latestFolder = GetLatestFolder(testRoot);
      if (string.IsNullOrEmpty(latestFolder) || !Directory.Exists(latestFolder))
      {
        logger.SendMessage(TestMessageLevel.Informational, $"RCA Test Adapter: No test deploy folder found under {testRoot}");
        logger.SendMessage(TestMessageLevel.Informational, "RCA Test Adapter: Test discovery completed (0 tests)");
        return;
      }

      logger.SendMessage(TestMessageLevel.Informational, $"RCA Test Adapter: Latest test folder: {latestFolder}");

      var testAssembly = Path.Combine(latestFolder, "Rca.Integration.Revit.Tests.dll");
      if (!File.Exists(testAssembly))
      {
        logger.SendMessage(TestMessageLevel.Informational, $"RCA Test Adapter: Test assembly not found: {testAssembly}");
        logger.SendMessage(TestMessageLevel.Informational, "RCA Test Adapter: Test discovery completed (0 tests)");
        return;
      }

      try
      {
        var testCases = NUnitTestDiscoverer.FindTestsInAssembly(testAssembly);
        foreach (var testCase in testCases)
        {
          // Source must be one of the 'sources' provided by VSTest, otherwise tests may be dropped.
          testCase.Source = hostSource;
          // Pass the real runtime DLL path via a custom property for the executor
          testCase.SetPropertyValue(AdapterProperties.RuntimeAssemblyPath, testAssembly);
          // Optional marker to differentiate in Test Explorer if needed
          testCase.Traits.Add(new Trait("Adapter", "RCA"));

          discoverySink.SendTestCase(testCase);
        }
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

  private static string GetLatestFolder(string root)
  {
    try
    {
      if (!Directory.Exists(root)) return string.Empty;
      var di = new DirectoryInfo(root);
      var latest = di.GetDirectories().OrderByDescending(d => d.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
      return latest?.FullName ?? string.Empty;
    }
    catch
    {
      return string.Empty;
    }
  }
}
