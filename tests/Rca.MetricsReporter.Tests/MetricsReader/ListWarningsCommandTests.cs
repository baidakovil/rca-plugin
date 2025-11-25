namespace Rca.MetricsReporter.Tests.MetricsReader;

using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Rca.Tools.MetricsReporter.MetricsReader.Commands;
using Rca.Tools.MetricsReporter.MetricsReader.Settings;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Integration-style tests for <see cref="ListWarningsCommand"/>.
/// </summary>
[TestFixture]
[Category("Unit")]
[Parallelizable(ParallelScope.None)]
internal sealed class ListWarningsCommandTests : MetricsReaderCommandTestsBase
{
  [Test]
  public async Task ExecuteAsync_WhenViolationsPresent_ReturnsSortedList()
  {
    // Arrange
    var report = MetricsReaderCommandTestData.CreateReport(new[]
    {
      MetricsReaderCommandTestData.CreateTypeNode("Rca.Loader.Services.Warning", 12, ThresholdStatus.Warning),
      MetricsReaderCommandTestData.CreateTypeNode("Rca.Loader.Services.ErrorMinor", 30, ThresholdStatus.Error),
      MetricsReaderCommandTestData.CreateTypeNode("Rca.Loader.Services.ErrorMajor", 40, ThresholdStatus.Error)
    });

    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Loader.Services");

    // Act
    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ListWarningsCommand>(settings)
      .ConfigureAwait(false);

    // Assert
    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    var rows = json.RootElement.EnumerateArray().ToList();
    rows.Should().HaveCount(3);
    rows[0].GetProperty("symbolFqn").GetString().Should().Be("Rca.Loader.Services.ErrorMajor");
    rows[1].GetProperty("symbolFqn").GetString().Should().Be("Rca.Loader.Services.ErrorMinor");
    rows[2].GetProperty("symbolFqn").GetString().Should().Be("Rca.Loader.Services.Warning");
  }

  [Test]
  public async Task ExecuteAsync_WhenSuppressedIgnored_DoesNotReturnSuppressedEntries()
  {
    // Arrange
    const string suppressedFqn = "Rca.Loader.Services.SuppressedType";
    var suppressedInfo = new SuppressedSymbolInfo
    {
      FullyQualifiedName = suppressedFqn,
      Metric = MetricIdentifier.RoslynCyclomaticComplexity.ToString(),
      RuleId = "CA1502",
      FilePath = "src/Rca.Loader/SuppressedType.cs"
    };

    var report = MetricsReaderCommandTestData.CreateReport(
      new[]
      {
        MetricsReaderCommandTestData.CreateTypeNode(suppressedFqn, 50, ThresholdStatus.Error),
        MetricsReaderCommandTestData.CreateTypeNode("Rca.Loader.Services.ActiveType", 25, ThresholdStatus.Warning)
      },
      new[] { suppressedInfo });

    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Loader.Services");

    // Act
    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ListWarningsCommand>(settings)
      .ConfigureAwait(false);

    // Assert
    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    var rows = json.RootElement.EnumerateArray().ToList();
    rows.Should().HaveCount(1);
    rows[0].GetProperty("symbolFqn").GetString().Should().Be("Rca.Loader.Services.ActiveType");
  }

  [Test]
  public async Task ExecuteAsync_WhenIncludeSuppressedTrue_ReturnsSuppressedEntries()
  {
    // Arrange
    const string suppressedFqn = "Rca.Loader.Services.SuppressedType";
    var suppressedInfo = new SuppressedSymbolInfo
    {
      FullyQualifiedName = suppressedFqn,
      Metric = MetricIdentifier.RoslynCyclomaticComplexity.ToString(),
      RuleId = "CA1502",
      FilePath = "src/Rca.Loader/SuppressedType.cs"
    };

    var report = MetricsReaderCommandTestData.CreateReport(
      new[]
      {
        MetricsReaderCommandTestData.CreateTypeNode(suppressedFqn, 50, ThresholdStatus.Error)
      },
      new[] { suppressedInfo });

    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Loader.Services", includeSuppressed: true);

    // Act
    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ListWarningsCommand>(settings)
      .ConfigureAwait(false);

    // Assert
    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.GetArrayLength().Should().Be(1);
    json.RootElement[0].GetProperty("symbolFqn").GetString().Should().Be(suppressedFqn);
    json.RootElement[0].GetProperty("isSuppressed").GetBoolean().Should().BeTrue();
  }

  [Test]
  public async Task ExecuteAsync_WithMemberSymbolKind_ReturnsMemberViolations()
  {
    // Arrange
    var member = MetricsReaderCommandTestData.CreateMemberNode(
      "Rca.Loader.Services.MemberType.Execute(...)", 30, ThresholdStatus.Error);
    var type = MetricsReaderCommandTestData.CreateTypeNode(
      "Rca.Loader.Services.MemberType",
      5,
      ThresholdStatus.Success,
      new[] { member });
    var report = MetricsReaderCommandTestData.CreateReport(new[] { type });

    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Loader.Services", symbolKind: MetricsReaderSymbolKind.Member);

    // Act
    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ListWarningsCommand>(settings)
      .ConfigureAwait(false);

    // Assert
    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.GetArrayLength().Should().Be(1);
    json.RootElement[0].GetProperty("symbolType").GetString().Should().Be("Member");
    json.RootElement[0].GetProperty("symbolFqn").GetString().Should().Contain("Execute(...)");
  }

  [Test]
  public async Task ExecuteAsync_WithThresholdOverride_EmitsOverrideValue()
  {
    // Arrange
    var report = MetricsReaderCommandTestData.CreateReport(new[]
    {
      MetricsReaderCommandTestData.CreateTypeNode("Rca.Loader.Services.Target", 12, ThresholdStatus.Warning)
    });

    var reportPath = WriteReport(report);
    var overridePath = WriteThresholdOverride(5, 6);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Loader.Services", thresholdsFile: overridePath);

    // Act
    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ListWarningsCommand>(settings)
      .ConfigureAwait(false);

    // Assert
    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    var row = json.RootElement[0];
    row.GetProperty("threshold").GetDecimal().Should().Be(5);
    row.GetProperty("thresholdKind").GetString().Should().Be("Warning");
  }

  [Test]
  public async Task ExecuteAsync_WhenNamespaceDoesNotMatch_ReturnsEmptyArray()
  {
    // Arrange
    var report = MetricsReaderCommandTestData.CreateReport(new[]
    {
      MetricsReaderCommandTestData.CreateTypeNode("Rca.Loader.Services.SomeType", 40, ThresholdStatus.Error)
    });

    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Other.Namespace");

    // Act
    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ListWarningsCommand>(settings)
      .ConfigureAwait(false);

    // Assert
    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.GetArrayLength().Should().Be(0);
  }

  [Test]
  public async Task ExecuteAsync_WhenNoViolations_ReturnsEmptyArray()
  {
    // Arrange
    var report = MetricsReaderCommandTestData.CreateReport(new[]
    {
      MetricsReaderCommandTestData.CreateTypeNode("Rca.Loader.Services.Clean", 5, ThresholdStatus.Success)
    });
    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Loader.Services");

    // Act
    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ListWarningsCommand>(settings)
      .ConfigureAwait(false);

    // Assert
    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.GetArrayLength().Should().Be(0);
  }
}


