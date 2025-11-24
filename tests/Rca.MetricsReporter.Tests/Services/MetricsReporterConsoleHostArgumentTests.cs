namespace Rca.MetricsReporter.Tests.Services;

using System.IO;
using FluentAssertions;
using NUnit.Framework;
using Rca.Tools.MetricsReporter;

/// <summary>
/// Verifies that command-line arguments are correctly mapped to <see cref="Rca.Tools.MetricsReporter.Services.MetricsReporterOptions"/>.
/// </summary>
[TestFixture]
[Category("Unit")]
public sealed class MetricsReporterConsoleHostArgumentTests
{
  /// <summary>
  /// Ensures that the presence of <c>--replace-baseline</c> on the command line
  /// sets <see cref="Rca.Tools.MetricsReporter.Services.MetricsReporterOptions.ReplaceMetricsBaseline"/> to <see langword="true"/>.
  /// </summary>
  [Test]
  public void ParseArguments_WithReplaceBaselineFlag_SetsReplaceMetricsBaselineTrue()
  {
    // Arrange
    var host = new MetricsReporterConsoleHost(TextWriter.Null);
    var args = new[]
    {
      "--metrics-dir", "c:\\temp\\metrics",
      "--output-json", "c:\\temp\\metrics\\report.json",
      "--baseline", "c:\\temp\\metrics\\baseline.json",
      "--replace-baseline"
    };

    // Act
    var options = host.ParseArguments(args);

    // Assert
    options.ReplaceMetricsBaseline.Should().BeTrue();
  }

  /// <summary>
  /// Ensures that when <c>--replace-baseline</c> is absent,
  /// <see cref="Rca.Tools.MetricsReporter.Services.MetricsReporterOptions.ReplaceMetricsBaseline"/> stays <see langword="false"/>.
  /// </summary>
  [Test]
  public void ParseArguments_WithoutReplaceBaselineFlag_SetsReplaceMetricsBaselineFalse()
  {
    // Arrange
    var host = new MetricsReporterConsoleHost(TextWriter.Null);
    var args = new[]
    {
      "--metrics-dir", "c:\\temp\\metrics",
      "--output-json", "c:\\temp\\metrics\\report.json",
      "--baseline", "c:\\temp\\metrics\\baseline.json"
    };

    // Act
    var options = host.ParseArguments(args);

    // Assert
    options.ReplaceMetricsBaseline.Should().BeFalse();
  }
}


