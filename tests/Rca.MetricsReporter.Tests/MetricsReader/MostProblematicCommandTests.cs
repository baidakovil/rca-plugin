namespace Rca.MetricsReporter.Tests.MetricsReader;

using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Rca.Tools.MetricsReporter.MetricsReader.Commands;
using Rca.Tools.MetricsReporter.MetricsReader.Settings;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Integration-style tests for <see cref="MostProblematicCommand"/>.
/// </summary>
[TestFixture]
[Category("Unit")]
[Parallelizable(ParallelScope.None)]
internal sealed class MostProblematicCommandTests : MetricsReaderCommandTestsBase
{
  [Test]
  public async Task ExecuteAsync_WhenErrorAndWarningExist_ReturnsErrorSymbol()
  {
    // Arrange
    var report = CreateReport(
      CreateTypeNode("Rca.Loader.Services.WarningService", 12, ThresholdStatus.Warning),
      CreateTypeNode("Rca.Loader.Services.ErrorService", 30, ThresholdStatus.Error));

    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Loader.Services");

    // Act
    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<MostProblematicCommand>(settings)
      .ConfigureAwait(false);

    // Assert
    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    var root = json.RootElement;
    root.GetProperty("symbolFqn").GetString().Should().Be("Rca.Loader.Services.ErrorService");
    root.GetProperty("status").GetString().Should().Be("Error");
  }

  [Test]
  public async Task ExecuteAsync_WhenOnlyWarnings_SelectsLargestMagnitude()
  {
    // Arrange
    var report = CreateReport(
      CreateTypeNode("Rca.Loader.Services.LowWarning", 12, ThresholdStatus.Warning),
      CreateTypeNode("Rca.Loader.Services.HighWarning", 18, ThresholdStatus.Warning));

    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Loader.Services");

    // Act
    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<MostProblematicCommand>(settings)
      .ConfigureAwait(false);

    // Assert
    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.GetProperty("symbolFqn").GetString().Should().Be("Rca.Loader.Services.HighWarning");
  }

  [Test]
  public async Task ExecuteAsync_WhenSuppressedSymbolsNotIncluded_SkipsSuppressedEntries()
  {
    // Arrange
    const string suppressedFqn = "Rca.Loader.Services.SuppressedService";
    var suppressedInfo = new SuppressedSymbolInfo
    {
      FullyQualifiedName = suppressedFqn,
      Metric = MetricIdentifier.RoslynCyclomaticComplexity.ToString(),
      RuleId = "CA1502",
      FilePath = "src/Rca.Loader/SuppressedService.cs"
    };

    var report = CreateReport(
      new[]
      {
        CreateTypeNode(suppressedFqn, 40, ThresholdStatus.Error),
        CreateTypeNode("Rca.Loader.Services.ActiveService", 25, ThresholdStatus.Warning)
      },
      new[] { suppressedInfo });

    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Loader.Services");

    // Act
    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<MostProblematicCommand>(settings)
      .ConfigureAwait(false);

    // Assert
    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.GetProperty("symbolFqn").GetString().Should().Be("Rca.Loader.Services.ActiveService");
  }

  [Test]
  public async Task ExecuteAsync_WhenIncludeSuppressedTrue_ReturnsSuppressedEntry()
  {
    // Arrange
    const string suppressedFqn = "Rca.Loader.Services.SuppressedService";
    var suppressedInfo = new SuppressedSymbolInfo
    {
      FullyQualifiedName = suppressedFqn,
      Metric = MetricIdentifier.RoslynCyclomaticComplexity.ToString(),
      RuleId = "CA1502",
      FilePath = "src/Rca.Loader/SuppressedService.cs"
    };

    var report = CreateReport(
      new[]
      {
        CreateTypeNode(suppressedFqn, 40, ThresholdStatus.Error),
        CreateTypeNode("Rca.Loader.Services.ActiveService", 25, ThresholdStatus.Warning)
      },
      new[] { suppressedInfo });

    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Loader.Services", includeSuppressed: true);

    // Act
    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<MostProblematicCommand>(settings)
      .ConfigureAwait(false);

    // Assert
    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.GetProperty("symbolFqn").GetString().Should().Be(suppressedFqn);
    json.RootElement.GetProperty("isSuppressed").GetBoolean().Should().BeTrue();
  }

  [Test]
  public async Task ExecuteAsync_WhenNoViolations_ReturnsNullPayload()
  {
    // Arrange
    var report = CreateReport(
      CreateTypeNode("Rca.Loader.Services.OkService", 8, ThresholdStatus.Success));

    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Loader.Services");

    // Act
    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<MostProblematicCommand>(settings)
      .ConfigureAwait(false);

    // Assert
    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.ValueKind.Should().Be(JsonValueKind.Null);
  }

  [Test]
  public async Task ExecuteAsync_WithMemberSymbolKind_ReturnsMemberViolation()
  {
    // Arrange
    var member = CreateMemberNode("Rca.Loader.Services.MemberService.Process(...)", 30, ThresholdStatus.Error);
    var type = CreateTypeNode("Rca.Loader.Services.MemberService", 5, ThresholdStatus.Success, members: new[] { member });
    var report = CreateReport(type);

    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(
      reportPath,
      "Rca.Loader.Services  ", // Intentional whitespace to ensure trimming
      symbolKind: MetricsReaderSymbolKind.Member);

    // Act
    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<MostProblematicCommand>(settings)
      .ConfigureAwait(false);

    // Assert
    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.GetProperty("symbolFqn").GetString().Should().Be("Rca.Loader.Services.MemberService.Process(...)");
    json.RootElement.GetProperty("symbolType").GetString().Should().Be("Member");
  }

  [Test]
  public async Task ExecuteAsync_WithThresholdOverride_UsesOverrideThresholdValue()
  {
    // Arrange
    var report = CreateReport(
      CreateTypeNode("Rca.Loader.Services.ThresholdTarget", 6, ThresholdStatus.Warning));

    var reportPath = WriteReport(report);
    var overridePath = WriteThresholdOverride(5, 6);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Loader.Services", thresholdsFile: overridePath);

    // Act
    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<MostProblematicCommand>(settings)
      .ConfigureAwait(false);

    // Assert
    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.GetProperty("threshold").GetDecimal().Should().Be(5);
  }

  private static MetricsReport CreateReport(params TypeMetricsNode[] types)
    => MetricsReaderCommandTestData.CreateReport(types);

  private static MetricsReport CreateReport(
    IEnumerable<TypeMetricsNode> types,
    IEnumerable<SuppressedSymbolInfo> suppressed)
    => MetricsReaderCommandTestData.CreateReport(types, suppressed);

  private static TypeMetricsNode CreateTypeNode(
    string fullyQualifiedName,
    decimal value,
    ThresholdStatus status,
    IEnumerable<MemberMetricsNode>? members = null)
    => MetricsReaderCommandTestData.CreateTypeNode(fullyQualifiedName, value, status, members);

  private static MemberMetricsNode CreateMemberNode(string fullyQualifiedName, decimal value, ThresholdStatus status)
    => MetricsReaderCommandTestData.CreateMemberNode(fullyQualifiedName, value, status);

}


