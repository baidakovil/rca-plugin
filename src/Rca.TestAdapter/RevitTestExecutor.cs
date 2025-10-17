using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IO;
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
            
            // Group tests by runtime assembly path if available, otherwise by source
            var testsByGroup = tests.GroupBy(test => 
                test.GetPropertyValue(AdapterProperties.RuntimeAssemblyPath, defaultValue: test.Source));
            
            foreach (var group in testsByGroup)
            {
                if (cancelled)
                {
                    break;
                }
                
                try
                {
                    var assemblyPath = group.Key; // Prefer runtime path
                    var sourceTests = group.ToList();
                    
                    frameworkHandle.SendMessage(TestMessageLevel.Informational, 
                        $"RCA Test Adapter: Executing {sourceTests.Count} tests from {assemblyPath}");
                    
                    var pipeClient = new RevitPipeClient();
                    var results = pipeClient.ExecuteTests(assemblyPath, sourceTests, defaultTimeout);
                    
                    // Process results
                    foreach (var result in results)
                    {
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
            var discoveredTests = new List<TestCase>();
            
            foreach (var source in sources)
            {
                try
                {
                    var tests = NUnitTestDiscoverer.FindTestsInAssembly(source);
                    // Annotate source-discovered tests with runtime path if this source is the runtime test assembly
                    foreach (var t in tests)
                    {
                        if (string.Equals(Path.GetFileName(t.Source), "Rca.Integration.Revit.Tests.dll", StringComparison.OrdinalIgnoreCase))
                        {
                            var runtimeRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RCA", "Runtime");
                            var latest = new DirectoryInfo(runtimeRoot).GetDirectories().OrderByDescending(d => d.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault()?.FullName ?? string.Empty;
                            var runtimeTestDll = string.IsNullOrEmpty(latest) ? t.Source : Path.Combine(latest, "Rca.Integration.Revit.Tests.dll");
                            t.SetPropertyValue(AdapterProperties.RuntimeAssemblyPath, runtimeTestDll);
                        }
                    }
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
