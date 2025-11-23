using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Rca.TestAdapter;

/// <summary>
/// Client for communicating with the Revit pipe server.
/// </summary>
public class RevitPipeClient
{
  /// <summary>
  /// Executes tests through the pipe server.
  /// </summary>
  /// <param name="assemblyPath">Path to the test assembly.</param>
  /// <param name="tests">The tests to execute.</param>
  /// <param name="timeout">Timeout for the operation.</param>
  /// <returns>The test results.</returns>
  public List<RevitTestResult> ExecuteTests(string assemblyPath, List<TestCase> tests, TimeSpan timeout)
  {
    if (tests is null)
      throw new ArgumentNullException(nameof(tests));

    var timeoutMs = (int)timeout.TotalMilliseconds;
    Console.WriteLine($"DEBUG: ExecuteTests starting for {tests.Count} tests in {assemblyPath}");

    try
    {
      var payload = CreateTestPayload(assemblyPath, tests);
      var serializedPayload = JsonSerializer.Serialize(payload);

      var command = new PipeCommand
      {
        Command = "RUN_TESTS",
        Payload = serializedPayload,
      };

      Console.WriteLine($"DEBUG: Sending RUN_TESTS command (payload length: {serializedPayload.Length})");
      var response = NamedPipeJsonClient.SendCommand(Constants.CommandPipeName, command, timeoutMs);

      if (response is null)
      {
        Console.WriteLine("DEBUG: No response received from pipe server for RUN_TESTS command");
        return new List<RevitTestResult>();
      }

      Console.WriteLine($"DEBUG: Received test execution response (status: {response.Status}, message length: {response.Message?.Length ?? 0})");

      return ProcessResponse(response);
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

  private List<RevitTestResult> ProcessResponse(PipeResponse? response)
  {
    if (response?.Status != "OK" || string.IsNullOrEmpty(response.Message))
    {
      return new List<RevitTestResult>();
    }

    try
    {
      var results = JsonSerializer.Deserialize<List<RevitTestResult>>(response.Message);
      Console.WriteLine($"DEBUG: Deserialized {results?.Count ?? 0} test results");
      return results ?? new List<RevitTestResult>();
    }
    catch (JsonException)
    {
      return new List<RevitTestResult>();
    }
  }
}
