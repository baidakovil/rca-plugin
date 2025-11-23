using System.Text.Json.Serialization;

namespace Rca.TestAdapter;

/// <summary>
/// Command sent through the named pipe.
/// </summary>
public class PipeCommand
{
  /// <summary>
  /// Gets or sets the command name.
  /// </summary>
  [JsonPropertyName("Command")]
  public string Command { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the command payload.
  /// </summary>
  [JsonPropertyName("Payload")]
  public string? Payload { get; set; }
}

/// <summary>
/// Response received from the named pipe.
/// </summary>
public class PipeResponse
{
  /// <summary>
  /// Gets or sets the response status.
  /// </summary>
  [JsonPropertyName("Status")]
  public string Status { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the response message.
  /// </summary>
  [JsonPropertyName("Message")]
  public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Request to execute a test.
/// </summary>
public class RevitTestRequest
{
  /// <summary>
  /// Gets or sets the fully qualified name of the test.
  /// </summary>
  [JsonPropertyName("FullyQualifiedName")]
  public string FullyQualifiedName { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the display name of the test.
  /// </summary>
  [JsonPropertyName("DisplayName")]
  public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// Result of a test execution.
/// </summary>
public class RevitTestResult
{
  /// <summary>
  /// Gets or sets the fully qualified name of the test.
  /// </summary>
  [JsonPropertyName("FullyQualifiedName")]
  public string FullyQualifiedName { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the display name of the test.
  /// </summary>
  [JsonPropertyName("DisplayName")]
  public string DisplayName { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the outcome of the test.
  /// </summary>
  [JsonPropertyName("Outcome")]
  public string Outcome { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the error message.
  /// </summary>
  [JsonPropertyName("ErrorMessage")]
  public string ErrorMessage { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the error stack trace.
  /// </summary>
  [JsonPropertyName("ErrorStackTrace")]
  public string ErrorStackTrace { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the duration in milliseconds.
  /// </summary>
  [JsonPropertyName("DurationInMilliseconds")]
  public double DurationInMilliseconds { get; set; }

  /// <summary>
  /// Gets or sets the start time in Unix milliseconds.
  /// </summary>
  [JsonPropertyName("StartTimeUnixMs")]
  public long StartTimeUnixMs { get; set; }

  /// <summary>
  /// Gets or sets the end time in Unix milliseconds.
  /// </summary>
  [JsonPropertyName("EndTimeUnixMs")]
  public long EndTimeUnixMs { get; set; }

  /// <summary>
  /// Gets or sets the messages.
  /// </summary>
  [JsonPropertyName("Messages")]
  public List<TestMessage> Messages { get; set; } = [];
}

/// <summary>
/// Message from test execution.
/// </summary>
public class TestMessage
{
  /// <summary>
  /// Gets or sets the message level.
  /// </summary>
  [JsonPropertyName("Level")]
  public string Level { get; set; } = "Informational";

  /// <summary>
  /// Gets or sets the message text.
  /// </summary>
  [JsonPropertyName("Text")]
  public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Payload for test execution.
/// </summary>
public class TestExecutionPayload
{
  /// <summary>
  /// Gets or sets the assembly path.
  /// </summary>
  [JsonPropertyName("AssemblyPath")]
  public string AssemblyPath { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the tests to execute.
  /// </summary>
  [JsonPropertyName("Tests")]
  public List<RevitTestRequest> Tests { get; set; } = [];
}


