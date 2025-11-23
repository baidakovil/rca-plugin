using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;

namespace Rca.TestAdapter;

/// <summary>
/// Helper for sending JSON-encoded <see cref="PipeCommand"/> messages over a named pipe
/// and receiving a single <see cref="PipeResponse"/> reply.
/// </summary>
/// <remarks>
/// This class centralizes the low-level pipe and JSON handling logic so that higher-level
/// components such as <see cref="RevitPipeClient"/> and <see cref="RevitTestInitializer"/>
/// can focus on protocol semantics instead of stream management. This reduces coupling
/// and duplication around pipe usage.
/// </remarks>
internal static class NamedPipeJsonClient
{
  /// <summary>
  /// Sends a command to the specified named pipe and waits for a single JSON response.
  /// </summary>
  /// <param name="pipeName">The name of the pipe to connect to.</param>
  /// <param name="command">The command to send.</param>
  /// <param name="timeoutMs">Connection timeout in milliseconds.</param>
  /// <returns>
  /// The deserialized <see cref="PipeResponse"/> if a valid JSON response was received;
  /// otherwise <see langword="null"/> when the pipe could not be connected or the response
  /// payload was empty or invalid JSON.
  /// </returns>
  public static PipeResponse? SendCommand(string pipeName, PipeCommand command, int timeoutMs)
  {
    if (string.IsNullOrWhiteSpace(pipeName))
      throw new ArgumentException("Pipe name must be provided.", nameof(pipeName));
    if (command is null)
      throw new ArgumentNullException(nameof(command));

    NamedPipeClientStream? pipeClient = null;
    StreamWriter? writer = null;
    StreamReader? reader = null;

    try
    {
      pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.None);
      pipeClient.Connect(timeoutMs);

      if (!pipeClient.IsConnected)
      {
        return null;
      }

      writer = new StreamWriter(pipeClient) { AutoFlush = true };
      reader = new StreamReader(pipeClient);

      var requestJson = JsonSerializer.Serialize(command);
      writer.WriteLine(requestJson);

      var responseJson = reader.ReadLine();
      if (string.IsNullOrEmpty(responseJson))
      {
        return null;
      }

      return JsonSerializer.Deserialize<PipeResponse>(responseJson);
    }
    catch (JsonException)
    {
      // Treat malformed JSON as "no response" for callers; they can decide how to handle it.
      return null;
    }
    finally
    {
      try { writer?.Dispose(); } catch { }
      try { reader?.Dispose(); } catch { }
      try
      {
        if (pipeClient?.IsConnected == true)
        {
          pipeClient.Close();
        }

        pipeClient?.Dispose();
      }
      catch
      {
        // Ignore cleanup failures.
      }
    }
  }
}


