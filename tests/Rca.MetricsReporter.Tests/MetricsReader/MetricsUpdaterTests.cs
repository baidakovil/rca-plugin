namespace Rca.MetricsReporter.Tests.MetricsReader;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Rca.Tools.MetricsReporter.MetricsReader.Services;

/// <summary>
/// Tests for <see cref="MetricsUpdater"/>.
/// </summary>
[TestFixture]
[Category("Unit")]
internal sealed class MetricsUpdaterTests
{
  [Test]
  public async Task UpdateAsync_UsesProjectMsbuildInvocation_WhenUpdateFlagSpecified()
  {
    using var sandbox = new TempDirectory();
    var solutionPath = Path.Combine(sandbox.Path, "rca-plugin.sln");
    File.WriteAllText(solutionPath, string.Empty);

    var testsProjectDir = Path.Combine(sandbox.Path, "tests", "Rca.MetricsReporter.Tests");
    Directory.CreateDirectory(testsProjectDir);
    var testsProjectPath = Path.Combine(testsProjectDir, "Rca.MetricsReporter.Tests.csproj");
    File.WriteAllText(testsProjectPath, "<Project />");

    var updater = new TestMetricsUpdater(solutionPath);
    await updater.UpdateAsync(CancellationToken.None).ConfigureAwait(false);

    updater.CapturedProjectPath.Should().Be(testsProjectPath);
    updater.CapturedArguments.Should().Be($"msbuild \"{testsProjectPath}\" /t:Build /p:GenerateMetricsDashboard=true /p:BuildProjectReferences=false /p:SkipMetricsReporterBuild=true /p:RoslynMetricsEnabled=true");
    updater.CapturedCoverageArguments.Should().Be($"msbuild \"{testsProjectPath}\" /t:CollectCoverage");
  }

  private sealed class TestMetricsUpdater(string solutionPath) : MetricsUpdater(solutionPath)
  {
    public string? CapturedProjectPath { get; private set; }

    public string? CapturedArguments { get; private set; }

    public string? CapturedCoverageArguments { get; private set; }

    protected override ProcessStartInfo CreateStartInfo(string projectPath, string solutionDirectory)
    {
      var startInfo = base.CreateStartInfo(projectPath, solutionDirectory);
      CapturedProjectPath = projectPath;
      CapturedArguments = startInfo.Arguments;

      var shell = Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe";
      startInfo.FileName = shell;
      startInfo.Arguments = "/c exit 0";
      return startInfo;
    }

    protected override ProcessStartInfo CreateCoverageStartInfo(string projectPath, string solutionDirectory)
    {
      var startInfo = base.CreateCoverageStartInfo(projectPath, solutionDirectory);
      CapturedCoverageArguments = startInfo.Arguments;

      var shell = Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe";
      startInfo.FileName = shell;
      startInfo.Arguments = "/c exit 0";
      return startInfo;
    }
  }

  private sealed class TempDirectory : IDisposable
  {
    public TempDirectory()
    {
      Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"metrics-updater-tests-{Guid.NewGuid():N}");
      Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
      try
      {
        if (Directory.Exists(Path))
        {
          Directory.Delete(Path, recursive: true);
        }
      }
      catch
      {
        // ignore cleanup failures
      }
    }
  }
}
