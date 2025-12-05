namespace Rca.Tools.MetricsReporter.MetricsReader.Services;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Runs the MSBuild targets that refresh MetricsReport.g.json and collect code coverage.
/// </summary>
/// <remarks>
/// This updater runs two MSBuild targets in sequence:
/// 1. CollectCoverage target to collect code coverage (only runs if AltCoverEnabled=true in code-metrics.props)
/// 2. Build target with GenerateMetricsDashboard=true to regenerate metrics report, which includes coverage data from step 1
/// The CollectCoverage target condition ensures it only runs when AltCoverEnabled is true, so no explicit check is needed here.
/// Coverage must be collected first because GenerateMetricsDashboard includes coverage files in the consolidated metrics report.
/// </remarks>
internal class MetricsUpdater : IMetricsUpdater
{
  private readonly string _solutionPath;

  public MetricsUpdater(string solutionPath)
    => _solutionPath = solutionPath ?? throw new ArgumentNullException(nameof(solutionPath));

  /// <summary>
  /// Updates metrics by collecting code coverage (if enabled), then running GenerateMetricsDashboard target.
  /// </summary>
  /// <param name="cancellationToken">Cancellation token for async operations.</param>
  /// <remarks>
  /// Coverage collection is controlled by the AltCoverEnabled property in code-metrics.props.
  /// The CollectCoverage target will automatically skip if AltCoverEnabled=false.
  /// Coverage is collected first because GenerateMetricsDashboard includes coverage data in the consolidated metrics report.
  /// </remarks>
  public async Task UpdateAsync(CancellationToken cancellationToken)
  {
    var solutionDirectory = Path.GetDirectoryName(_solutionPath)
      ?? throw new InvalidOperationException($"Cannot resolve solution directory for '{_solutionPath}'.");

    var projectPath = ResolveMetricsProjectPath(solutionDirectory);

    // Step 1: Collect coverage (will automatically skip if AltCoverEnabled=false due to target condition)
    // This must run first to generate coverage files that will be included in the metrics dashboard
    var coverageStartInfo = CreateCoverageStartInfo(projectPath, solutionDirectory);
    await RunProcessAsync(coverageStartInfo, "Collecting code coverage...", "Coverage collected successfully.", cancellationToken).ConfigureAwait(false);

    // Step 2: Generate metrics dashboard (includes coverage data from Step 1)
    var startInfo = CreateStartInfo(projectPath, solutionDirectory);
    await RunProcessAsync(startInfo, "Updating metrics via GenerateMetricsDashboard...", "Metrics updated successfully.", cancellationToken).ConfigureAwait(false);
  }

  private async Task RunProcessAsync(ProcessStartInfo startInfo, string startMessage, string successMessage, CancellationToken cancellationToken)
  {
    using var process = new Process { StartInfo = startInfo };
    Console.WriteLine(startMessage);
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

    Console.WriteLine(successMessage);
  }

  /// <summary>
  /// Creates ProcessStartInfo for running the GenerateMetricsDashboard target.
  /// </summary>
  /// <param name="projectPath">Path to the project file.</param>
  /// <param name="solutionDirectory">Directory containing the solution.</param>
  /// <returns>ProcessStartInfo configured for MSBuild.</returns>
  protected virtual ProcessStartInfo CreateStartInfo(string projectPath, string solutionDirectory)
  {
    var arguments = $"msbuild \"{projectPath}\" /t:Build /p:GenerateMetricsDashboard=true /p:BuildProjectReferences=false /p:SkipMetricsReporterBuild=true /p:RoslynMetricsEnabled=true";
    return new ProcessStartInfo
    {
      FileName = "dotnet",
      Arguments = arguments,
      WorkingDirectory = solutionDirectory,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true
    };
  }

  /// <summary>
  /// Creates ProcessStartInfo for running the CollectCoverage target.
  /// </summary>
  /// <param name="projectPath">Path to the project file.</param>
  /// <param name="solutionDirectory">Directory containing the solution.</param>
  /// <returns>ProcessStartInfo configured for MSBuild.</returns>
  /// <remarks>
  /// The CollectCoverage target condition ensures it only runs when AltCoverEnabled=true (from code-metrics.props),
  /// so this will automatically skip if coverage collection is disabled. We don't pass AltCoverEnabled as a property
  /// to respect the value from code-metrics.props.
  /// </remarks>
  protected virtual ProcessStartInfo CreateCoverageStartInfo(string projectPath, string solutionDirectory)
  {
    var arguments = $"msbuild \"{projectPath}\" /t:CollectCoverage";
    return new ProcessStartInfo
    {
      FileName = "dotnet",
      Arguments = arguments,
      WorkingDirectory = solutionDirectory,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true
    };
  }

  private static string ResolveMetricsProjectPath(string solutionDirectory)
  {
    var projectPath = Directory.EnumerateFiles(solutionDirectory, "Rca.MetricsReporter.Tests.csproj", SearchOption.AllDirectories)
      .FirstOrDefault();
    if (string.IsNullOrWhiteSpace(projectPath))
    {
      throw new InvalidOperationException("Rca.MetricsReporter.Tests project file could not be located.");
    }

    return projectPath;
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
