using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using Rca.Generated;
using Rca.Loader.Infrastructure;
using Rca.Logging.Contracts;

/// <summary>
/// Simple test logger that writes to LocalApplicationData/RCA/TestLogs and also to Debug.
/// Used to surface messages from integration tests when TestContext output isn't available.
/// </summary>
public sealed class TestLogger : IDisposable
{
  private readonly StreamWriter _writer;
  private readonly string _path;
  private NamedPipeClientStream? _pipe;
  private StreamWriter? _pipeWriter;

  private TestLogger(string path, StreamWriter writer)
  {
    _path = path;
    _writer = writer;
  }

  public static TestLogger Start()
  {
    var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RCA", "Logs");
    Directory.CreateDirectory(dir);
    var file = Path.Combine(dir, $"integration-{DateTime.Now.ToString(LoaderConstants.TimestampPattern)}.log");
    var fs = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
    var writer = new StreamWriter(fs) { AutoFlush = true };
    var logger = new TestLogger(file, writer);
    logger.TryConnectPipe();
    return logger;
  }

  public void Log(string message)
  {
    var line = $"[{DateTime.Now:O}] {message}";
    try { _writer.WriteLine(line); } catch { }
    try { System.Diagnostics.Debug.WriteLine(line); } catch { }
    TrySendToPipe("Information", "Tests", message, null);
  }

  public void Dispose()
  {
    try { _pipeWriter?.Dispose(); } catch { }
    try { _pipe?.Dispose(); } catch { }
    try { _writer.Dispose(); } catch { }
  }

  private void TryConnectPipe()
  {
    try
    {
      _pipe = new NamedPipeClientStream(".", LoaderConstants.CommandPipeName, PipeDirection.Out, PipeOptions.Asynchronous);
      _pipe.Connect(2000); // 2s timeout
      _pipeWriter = new StreamWriter(_pipe) { AutoFlush = true };
      Log($"Connected to logging pipe '{LoaderConstants.CommandPipeName}'");
    }
    catch
    {
      // Ignore; file log still works
    }
  }

  private void TrySendToPipe(string level, string category, string message, Exception? ex)
  {
    try
    {
      if (_pipeWriter == null) return;
      var dto = new LogEntryDto
      {
        TimestampTicks = DateTime.Now.Ticks,
        Level = level,
        Category = category,
        Message = message,
        Exception = ex?.ToString(),
        RuntimeSessionId = "tests",
        SequenceId = 0,
        RuntimeProcessId = Environment.ProcessId,
        ALCInstanceId = null,
        IsFallback = false,
        Flags = 0,
        IsPing = false
      };
      var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
      _pipeWriter.WriteLine(json);
    }
    catch
    {
      // swallow
    }
  }
}


