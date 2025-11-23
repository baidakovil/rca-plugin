using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Rca.TestAdapter;

/// <summary>
/// Abstraction over the low-level pipe execution of tests.
/// </summary>
internal interface ITestExecutionTransport
{
  /// <summary>
  /// Executes the specified tests for the given assembly path and returns raw test results.
  /// </summary>
  /// <param name="assemblyPath">The test assembly path.</param>
  /// <param name="tests">Tests to execute.</param>
  /// <param name="timeout">Timeout for the operation.</param>
  /// <returns>List of raw <see cref="RevitTestResult"/> objects returned by the pipe server.</returns>
  List<RevitTestResult> Execute(string assemblyPath, IReadOnlyList<TestCase> tests, TimeSpan timeout);
}

/// <summary>
/// Default transport that uses <see cref="NamedPipeJsonClient"/> and the RCA test protocol
/// to execute tests in a running Revit instance.
/// </summary>
internal sealed class PipeTestExecutionTransport : ITestExecutionTransport
{
  [SuppressMessage(
      "Microsoft.Maintainability",
      "CA1506:Avoid excessive class coupling",
      Justification = "PipeTestExecutionTransport is responsible for low-level pipe transport concerns (NamedPipe, JSON, DTO mapping). Further splitting would fragment cohesive transport logic without improving design sustainability.")]
  public List<RevitTestResult> Execute(string assemblyPath, IReadOnlyList<TestCase> tests, TimeSpan timeout)
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

  private static TestExecutionPayload CreateTestPayload(string assemblyPath, IReadOnlyList<TestCase> tests)
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

  private static List<RevitTestResult> ProcessResponse(PipeResponse? response)
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


