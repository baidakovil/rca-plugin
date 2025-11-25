namespace Rca.Tools.MetricsReporter.MetricsReader.Services;

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Runs the MSBuild target that refreshes MetricsReport.g.json.
/// </summary>
internal sealed class MetricsUpdater
{
  private readonly string _solutionPath;

  public MetricsUpdater(string solutionPath)
    => _solutionPath = solutionPath ?? throw new ArgumentNullException(nameof(solutionPath));

  public async Task UpdateAsync(CancellationToken cancellationToken)
  {
    var solutionDirectory = Path.GetDirectoryName(_solutionPath)
      ?? throw new InvalidOperationException($"Cannot resolve solution directory for '{_solutionPath}'.");

    var arguments = $"msbuild \"{_solutionPath}\" /t:Rca.MetricsReporter.Tests /p:GenerateMetricsDashboard=true";
    var startInfo = new ProcessStartInfo
    {
      FileName = "dotnet",
      Arguments = arguments,
      WorkingDirectory = solutionDirectory,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true
    };

    using var process = new Process { StartInfo = startInfo };
    Console.WriteLine("Updating metrics via GenerateMetricsDashboard...");
    if (!process.Start())
    {
      throw new InvalidOperationException("Failed to start metrics update process.");
    }

    var stdOutTask = PumpAsync(process.StandardOutput, Console.Out, cancellationToken);
    var stdErrTask = PumpAsync(process.StandardError, Console.Error, cancellationToken);

    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
    await Task.WhenAll(stdOutTask, stdErrTask).ConfigureAwait(false);

    if (process.ExitCode != 0)
    {
      throw new InvalidOperationException($"Metrics update failed with exit code {process.ExitCode}.");
    }

    Console.WriteLine("Metrics updated successfully.");
  }

  private static async Task PumpAsync(StreamReader reader, TextWriter destination, CancellationToken cancellationToken)
  {
    var buffer = new char[4096];
    while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
    {
      var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
      if (read == 0)
      {
        break;
      }

      await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
    }
  }
}

