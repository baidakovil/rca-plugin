using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using System.Diagnostics;

namespace Rca.TestAdapter;

/// <summary>
/// Client for communicating with the Revit pipe server.
/// </summary>
public class RevitPipeClient
{
    private const int DefaultTimeoutMs = 60000; // 60 seconds
    
    /// <summary>
    /// Executes tests through the pipe server.
    /// </summary>
    /// <param name="assemblyPath">Path to the test assembly.</param>
    /// <param name="tests">The tests to execute.</param>
    /// <param name="timeout">Timeout for the operation.</param>
    /// <returns>The test results.</returns>
    public List<RevitTestResult> ExecuteTests(string assemblyPath, List<TestCase> tests, TimeSpan timeout)
    {
        var timeoutMs = (int)timeout.TotalMilliseconds;
        Console.WriteLine($"DEBUG: ExecuteTests starting for {tests.Count} tests in {assemblyPath}");
        
        try
        {
            // Create a fresh connection for test execution
            Console.WriteLine("DEBUG: Creating fresh pipe connection for test execution");
            using var pipeClient = new NamedPipeClientStream(".", Constants.PipeName, PipeDirection.InOut, PipeOptions.None);
            
            // Connect with timeout
            try
            {
                Console.WriteLine($"DEBUG: Connecting to pipe with timeout {timeoutMs}ms");
                pipeClient.Connect(timeoutMs);
            }
            catch (TimeoutException)
            {
                Console.WriteLine($"DEBUG: Connection timed out after {timeoutMs}ms");
                return new List<RevitTestResult>();
            }
            
            Console.WriteLine($"DEBUG: Connected to pipe, IsConnected={pipeClient.IsConnected}");
            
            // Create new StreamWriter/StreamReader
            using var writer = new StreamWriter(pipeClient) { AutoFlush = true };
            using var reader = new StreamReader(pipeClient);
            
            // Convert TestCase objects to RevitTestRequest objects
            var requests = new List<RevitTestRequest>();
            foreach (var test in tests)
            {
                requests.Add(new RevitTestRequest
                {
                    FullyQualifiedName = test.FullyQualifiedName,
                    DisplayName = test.DisplayName
                });
            }
            
            // Create the payload
            var payload = new TestExecutionPayload
            {
                AssemblyPath = assemblyPath,
                Tests = requests
            };
            
            // Serialize the payload
            var payloadJson = JsonSerializer.Serialize(payload);
            
            // Send the RUN_TESTS command
            var cmd = new PipeCommand
            {
                Command = "RUN_TESTS",
                Payload = payloadJson
            };
            
            var json = JsonSerializer.Serialize(cmd);
            Console.WriteLine($"DEBUG: Sending RUN_TESTS command (payload length: {payloadJson.Length})");
            writer.WriteLine(json);
            writer.Flush();
            
            // Read the response
            Console.WriteLine("DEBUG: Reading test execution response");
            var responseJson = reader.ReadLine();
            
            if (string.IsNullOrEmpty(responseJson))
            {
                Console.WriteLine("DEBUG: Received empty test execution response");
                return new List<RevitTestResult>();
            }
            
            Console.WriteLine($"DEBUG: Received test execution response (length: {responseJson.Length})");
            var response = JsonSerializer.Deserialize<PipeResponse>(responseJson);
            
            if (response == null || response.Status != "OK" || string.IsNullOrEmpty(response.Message))
            {
                Console.WriteLine($"DEBUG: Invalid test execution response status: {response?.Status}");
                return new List<RevitTestResult>();
            }
            
            try
            {
                var results = JsonSerializer.Deserialize<List<RevitTestResult>>(response.Message);
                Console.WriteLine($"DEBUG: Deserialized {results?.Count ?? 0} test results");
                return results ?? new List<RevitTestResult>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Error deserializing test results: {ex.Message}");
                Console.WriteLine($"DEBUG: Stack trace: {ex.StackTrace}");
                return new List<RevitTestResult>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Error executing tests: {ex.Message}");
            Console.WriteLine($"DEBUG: Stack trace: {ex.StackTrace}");
            return new List<RevitTestResult>();
        }
    }
}