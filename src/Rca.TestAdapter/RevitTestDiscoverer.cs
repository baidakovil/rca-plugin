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
    /// only the Rca.Integration.Revit.Tests.dll assembly.
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
        if (logger is null)
            throw new ArgumentNullException(nameof(logger));
        if (discoverySink is null)
            throw new ArgumentNullException(nameof(discoverySink));

        logger.SendMessage(TestMessageLevel.Informational, "RCA Test Adapter: Starting test discovery");

        try
        {
            var runtimeRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RCA", "Runtime");
            var latestFolder = GetLatestFolder(runtimeRoot);

            if (string.IsNullOrEmpty(latestFolder) || !Directory.Exists(latestFolder))
            {
                logger.SendMessage(TestMessageLevel.Informational, $"RCA Test Adapter: No runtime folder found under {runtimeRoot}");
                logger.SendMessage(TestMessageLevel.Informational, "RCA Test Adapter: Test discovery completed (0 tests)");
                return;
            }

            logger.SendMessage(TestMessageLevel.Informational, $"RCA Test Adapter: Latest runtime folder: {latestFolder}");

            var testAssemblyPath = Path.Combine(latestFolder, "Rca.Integration.Revit.Tests.dll");
            if (!File.Exists(testAssemblyPath))
            {
                logger.SendMessage(TestMessageLevel.Informational, $"RCA Test Adapter: Test assembly not found: {testAssemblyPath}");
                logger.SendMessage(TestMessageLevel.Informational, "RCA Test Adapter: Test discovery completed (0 tests)");
                return;
            }

            try
            {
                var testCases = NUnitTestDiscoverer.FindTestsInAssembly(testAssemblyPath);
                foreach (var testCase in testCases)
                {
                    discoverySink.SendTestCase(testCase);
                }
                logger.SendMessage(TestMessageLevel.Informational, $"RCA Test Adapter: Discovered {testCases.Count} tests in {testAssemblyPath}");
            }
            catch (Exception ex)
            {
                logger.SendMessage(TestMessageLevel.Error, $"RCA Test Adapter: Error discovering tests in {testAssemblyPath}: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            logger.SendMessage(TestMessageLevel.Error, $"RCA Test Adapter: Discovery error: {ex.Message}");
        }

        logger.SendMessage(TestMessageLevel.Informational, "RCA Test Adapter: Test discovery completed");
    }

    private static string GetLatestFolder(string runtimeRoot)
    {
        try
        {
            if (!Directory.Exists(runtimeRoot)) return string.Empty;
            var di = new DirectoryInfo(runtimeRoot);
            var latest = di.GetDirectories().OrderByDescending(d => d.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            return latest?.FullName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
