using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
    private readonly TimeSpan defaultTimeout = TimeSpan.FromMinutes(2);
    
    /// <summary>
    /// Initializes a new instance of the <see cref="RevitTestExecutor"/> class.
    /// </summary>
    public RevitTestExecutor()
    {
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
        
        try
        {
            frameworkHandle.SendMessage(TestMessageLevel.Informational, "RCA Test Adapter: Starting test execution");
            
            // Ensure Revit is initialized
            if (!RevitTestInitializer.EnsureRevitIsInitialized())
            {
                frameworkHandle.SendMessage(TestMessageLevel.Error, 
                    "RCA Test Adapter: Failed to initialize Revit. Make sure Revit is running with the RCA plugin loaded.");
                return;
            }
            
            // Group tests by source assembly
            var testsBySource = tests.GroupBy(test => test.Source);
            
            foreach (var sourceGroup in testsBySource)
            {
                if (cancelled)
                {
                    break;
                }
                
                try
                {
                    // Execute all tests from this source through the pipe
                    var sourcePath = sourceGroup.Key;
                    var sourceTests = sourceGroup.ToList();
                    
                    frameworkHandle.SendMessage(TestMessageLevel.Informational, 
                        $"RCA Test Adapter: Executing {sourceTests.Count} tests from {sourcePath}");
                    
                    var pipeClient = new RevitPipeClient();
                    var results = pipeClient.ExecuteTests(sourcePath, sourceTests, defaultTimeout);
                    
                    // Process results
                    foreach (var result in results)
                    {
                        // Convert the TestResult to a TestResult object and report it
                        var testCase = sourceTests.FirstOrDefault(t => 
                            t.FullyQualifiedName == result.FullyQualifiedName);
                        
                        if (testCase != null)
                        {
                            var testResult = new Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult(testCase)
                            {
                                Outcome = ConvertTestOutcome(result.Outcome),
                                ErrorMessage = result.ErrorMessage,
                                ErrorStackTrace = result.ErrorStackTrace,
                                DisplayName = result.DisplayName,
                                Duration = TimeSpan.FromMilliseconds(result.DurationInMilliseconds),
                                StartTime = DateTimeOffset.FromUnixTimeMilliseconds(result.StartTimeUnixMs),
                                EndTime = DateTimeOffset.FromUnixTimeMilliseconds(result.EndTimeUnixMs),
                            };
                            
                            // Add any extra properties or messages from the result
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
                    frameworkHandle.SendMessage(TestMessageLevel.Error, 
                        $"RCA Test Adapter: Error executing tests: {ex.Message}");
                    frameworkHandle.SendMessage(TestMessageLevel.Informational, 
                        $"RCA Test Adapter: Exception details: {ex}");
                }
            }
            
            frameworkHandle.SendMessage(TestMessageLevel.Informational, "RCA Test Adapter: Test execution completed");
        }
        finally
        {
            // No resources to clean up with this approach
        }
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
            // Discover tests in the sources, then run them
            var discoveredTests = new List<TestCase>();
            
            foreach (var source in sources)
            {
                try
                {
                    var tests = NUnitTestDiscoverer.FindTestsInAssembly(source);
                    discoveredTests.AddRange(tests);
                }
                catch (Exception ex)
                {
                    frameworkHandle.SendMessage(TestMessageLevel.Error, 
                        $"RCA Test Adapter: Error discovering tests in {source}: {ex.Message}");
                }
            }
            
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