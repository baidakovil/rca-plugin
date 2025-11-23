using System;
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Rca.TestAdapter;

/// <summary>
/// Client for communicating with the Revit pipe server.
/// </summary>
public class RevitPipeClient
{
  private readonly ITestExecutionTransport _transport;

  public RevitPipeClient()
      : this(new PipeTestExecutionTransport())
  {
  }

  internal RevitPipeClient(ITestExecutionTransport transport)
  {
    ArgumentNullException.ThrowIfNull(transport);
    _transport = transport;
  }

  /// <summary>
  /// Executes tests through the pipe server.
  /// </summary>
  /// <param name="assemblyPath">Path to the test assembly.</param>
  /// <param name="tests">The tests to execute.</param>
  /// <param name="timeout">Timeout for the operation.</param>
  /// <returns>The test results.</returns>
  public List<RevitTestResult> ExecuteTests(string assemblyPath, List<TestCase> tests, TimeSpan timeout)
  {
    ArgumentNullException.ThrowIfNull(tests);

    return _transport.Execute(assemblyPath, tests, timeout);
  }
}


