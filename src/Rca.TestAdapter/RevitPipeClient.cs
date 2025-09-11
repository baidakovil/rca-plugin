using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

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
        
        NamedPipeClientStream? pipeClient = null;
        StreamWriter? writer = null;
        StreamReader? reader = null;
        
        try
        {
            // Create a fresh connection for test execution
            Console.WriteLine("DEBUG: Creating fresh pipe connection for test execution");
            pipeClient = new NamedPipeClientStream(".", Constants.PipeName, PipeDirection.InOut, PipeOptions.None);
            
            // Connect with timeout
            Console.WriteLine($"DEBUG: Connecting to pipe with timeout {timeoutMs}ms");
            pipeClient.Connect(timeoutMs);
            Console.WriteLine($"DEBUG: Connected to pipe, IsConnected={pipeClient.IsConnected}");
            
            // Create new StreamWriter/StreamReader
            writer = new StreamWriter(pipeClient) { AutoFlush = true };
            reader = new StreamReader(pipeClient);
            
            var payload = CreateTestPayload(assemblyPath, tests);
            var command = new PipeCommand { Command = "RUN_TESTS", Payload = JsonSerializer.Serialize(payload) };
            
            Console.WriteLine($"DEBUG: Sending RUN_TESTS command (payload length: {JsonSerializer.Serialize(payload).Length})");
            writer.WriteLine(JsonSerializer.Serialize(command));
            
            Console.WriteLine("DEBUG: Reading test execution response");
            var responseJson = reader.ReadLine();
            
            Console.WriteLine($"DEBUG: Received test execution response (length: {responseJson?.Length ?? 0})");
            
            // Explicitly close streams before disposal
            writer.Close();
            reader.Close();
            
            return ProcessResponse(responseJson);
        }
        catch (TimeoutException)
        {
            Console.WriteLine($"DEBUG: Connection timed out after {timeoutMs}ms");
            return new List<RevitTestResult>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Error executing tests: {ex.Message}");
            return new List<RevitTestResult>();
        }
        finally
        {
            // Clean up resources in proper order
            try
            {
                writer?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Error disposing writer: {ex.Message}");
            }
            
            try
            {
                reader?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Error disposing reader: {ex.Message}");
            }
            
            try
            {
                if (pipeClient != null)
                {
                    if (pipeClient.IsConnected)
                    {
                        pipeClient.Close();
                    }
                    pipeClient.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Error disposing pipe client: {ex.Message}");
            }
        }
    }
    
    private TestExecutionPayload CreateTestPayload(string assemblyPath, List<TestCase> tests)
    {
        var requests = tests.Select(test => new RevitTestRequest
        {
            FullyQualifiedName = test.FullyQualifiedName,
            DisplayName = test.DisplayName
        }).ToList();
        
        return new TestExecutionPayload
        {
            AssemblyPath = assemblyPath,
            Tests = requests
        };
    }
    
    private List<RevitTestResult> ProcessResponse(string? responseJson)
    {
        if (string.IsNullOrEmpty(responseJson))
        {
            Console.WriteLine("DEBUG: Received empty test execution response");
            return new List<RevitTestResult>();
        }
        
        var response = JsonSerializer.Deserialize<PipeResponse>(responseJson);
        if (response?.Status != "OK" || string.IsNullOrEmpty(response.Message))
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
        catch (JsonException ex)
        {
            Console.WriteLine($"DEBUG: Error deserializing test results: {ex.Message}");
            return new List<RevitTestResult>();
        }
    }
}