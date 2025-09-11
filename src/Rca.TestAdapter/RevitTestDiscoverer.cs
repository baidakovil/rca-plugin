using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
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
    /// Discovers tests in the specified container.
    /// </summary>
    /// <param name="sources">The list of test containers.</param>
    /// <param name="discoveryContext">The discovery context.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="discoverySink">The discovery sink used to report discovered tests.</param>
    public void DiscoverTests(
        IEnumerable<string> sources,
        IDiscoveryContext discoveryContext,
        IMessageLogger logger,
        ITestCaseDiscoverySink discoverySink)
    {
        logger.SendMessage(TestMessageLevel.Informational, "RCA Test Adapter: Starting test discovery");

        foreach (var source in sources)
        {
            try
            {
                // Load the assembly and discover NUnit tests via reflection
                var testCases = NUnitTestDiscoverer.FindTestsInAssembly(source);
                
                foreach (var testCase in testCases)
                {
                    discoverySink.SendTestCase(testCase);
                }
                
                logger.SendMessage(TestMessageLevel.Informational, 
                    $"RCA Test Adapter: Discovered {testCases.Count} tests in {source}");
            }
            catch (Exception ex)
            {
                logger.SendMessage(TestMessageLevel.Error, 
                    $"RCA Test Adapter: Error discovering tests in {source}: {ex.Message}");
            }
        }
        
        logger.SendMessage(TestMessageLevel.Informational, "RCA Test Adapter: Test discovery completed");
    }
}