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
/// Integration-style tests for the readany command.
/// </summary>
[TestFixture]
[Category("Unit")]
[Parallelizable(ParallelScope.None)]
internal sealed class ReadAnyCommandTests : MetricsReaderCommandTestsBase
{
  [Test]
  public async Task ExecuteAsync_ShowAllTrue_ReturnsSortedList()
  {
    var report = MetricsReaderCommandTestData.CreateReport(new[]
    {
      MetricsReaderCommandTestData.CreateTypeNode("Rca.Loader.Services.Warning", 12, ThresholdStatus.Warning),
      MetricsReaderCommandTestData.CreateTypeNode("Rca.Loader.Services.ErrorMinor", 30, ThresholdStatus.Error),
      MetricsReaderCommandTestData.CreateTypeNode("Rca.Loader.Services.ErrorMajor", 40, ThresholdStatus.Error)
    });

    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Loader.Services", showAll: true);

    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ReadAnyCommand>(settings)
      .ConfigureAwait(false);

    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    var rows = json.RootElement.EnumerateArray().ToList();
    rows.Should().HaveCount(3);
    rows.Select(r => r.GetProperty("symbolFqn").GetString()).Should().ContainInOrder(
      "Rca.Loader.Services.ErrorMajor",
      "Rca.Loader.Services.ErrorMinor",
      "Rca.Loader.Services.Warning");
  }

  [Test]
  public async Task ExecuteAsync_ShowAllFalse_ReturnsSingleMostSevereSymbol()
  {
    var report = MetricsReaderCommandTestData.CreateReport(new[]
    {
      MetricsReaderCommandTestData.CreateTypeNode("Rca.Loader.Services.Warning", 12, ThresholdStatus.Warning),
      MetricsReaderCommandTestData.CreateTypeNode("Rca.Loader.Services.Error", 35, ThresholdStatus.Error)
    });

    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Loader.Services");

    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ReadAnyCommand>(settings)
      .ConfigureAwait(false);

    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
    json.RootElement.GetProperty("symbolFqn").GetString().Should().Be("Rca.Loader.Services.Error");
    json.RootElement.GetProperty("status").GetString().Should().Be("Error");
  }

  [Test]
  public async Task ExecuteAsync_IgnoresSuppressedSymbolsWhenIncludeSuppressedFalse()
  {
    const string suppressedFqn = "Rca.Loader.Services.SuppressedService";
    var suppressedInfo = new SuppressedSymbolInfo
    {
      FullyQualifiedName = suppressedFqn,
      Metric = MetricIdentifier.RoslynCyclomaticComplexity.ToString(),
      RuleId = "CA1502",
      FilePath = "src/Rca.Loader/SuppressedService.cs"
    };

    var report = MetricsReaderCommandTestData.CreateReport(
      new[]
      {
        MetricsReaderCommandTestData.CreateTypeNode(suppressedFqn, 40, ThresholdStatus.Error),
        MetricsReaderCommandTestData.CreateTypeNode("Rca.Loader.Services.Active", 25, ThresholdStatus.Warning)
      },
      new[] { suppressedInfo });

    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Loader.Services");

    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ReadAnyCommand>(settings)
      .ConfigureAwait(false);

    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.GetProperty("symbolFqn").GetString().Should().Be("Rca.Loader.Services.Active");
  }

  [Test]
  public async Task ExecuteAsync_ShowAllTrue_IncludesSuppressedEntriesWhenRequested()
  {
    const string suppressedFqn = "Rca.Loader.Services.Suppressed";
    var suppressedInfo = new SuppressedSymbolInfo
    {
      FullyQualifiedName = suppressedFqn,
      Metric = MetricIdentifier.RoslynCyclomaticComplexity.ToString(),
      RuleId = "CA1502",
      FilePath = "src/Rca.Loader/Suppressed.cs"
    };

    var report = MetricsReaderCommandTestData.CreateReport(
      new[]
      {
        MetricsReaderCommandTestData.CreateTypeNode(suppressedFqn, 40, ThresholdStatus.Error)
      },
      new[] { suppressedInfo });

    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Loader.Services", includeSuppressed: true, showAll: true);

    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ReadAnyCommand>(settings)
      .ConfigureAwait(false);

    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.GetArrayLength().Should().Be(1);
    json.RootElement[0].GetProperty("symbolFqn").GetString().Should().Be(suppressedFqn);
    json.RootElement[0].GetProperty("isSuppressed").GetBoolean().Should().BeTrue();
  }

  [Test]
  public async Task ExecuteAsync_MemberSymbolKind_ReturnsMembers()
  {
    var member = MetricsReaderCommandTestData.CreateMemberNode("Rca.Loader.Services.Type.Execute(...)", 30, ThresholdStatus.Error);
    var type = MetricsReaderCommandTestData.CreateTypeNode("Rca.Loader.Services.Type", 5, ThresholdStatus.Success, new[] { member });
    var report = MetricsReaderCommandTestData.CreateReport(new[] { type });

    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Loader.Services", symbolKind: MetricsReaderSymbolKind.Member, showAll: true);

    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ReadAnyCommand>(settings)
      .ConfigureAwait(false);

    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.GetArrayLength().Should().Be(1);
    json.RootElement[0].GetProperty("symbolType").GetString().Should().Be("Member");
    json.RootElement[0].GetProperty("symbolFqn").GetString().Should().Contain("Execute(...)");
  }

  [Test]
  public async Task ExecuteAsync_SymbolKindAny_WithAll_PrefersTypesBeforeMembers()
  {
    // Arrange: Create a member with higher priority (Error + higher magnitude) than the type
    // to verify that types are still listed first when SymbolKind is Any
    var member = MetricsReaderCommandTestData.CreateMemberNode("Rca.Loader.Services.MixedType.Execute(...)", 60, ThresholdStatus.Error);
    var type = MetricsReaderCommandTestData.CreateTypeNode("Rca.Loader.Services.MixedType", 30, ThresholdStatus.Error, new[] { member });
    var report = MetricsReaderCommandTestData.CreateReport(new[] { type });

    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(
      reportPath,
      "Rca.Loader.Services",
      symbolKind: MetricsReaderSymbolKind.Any,
      showAll: true);

    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ReadAnyCommand>(settings)
      .ConfigureAwait(false);

    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    var rows = json.RootElement.EnumerateArray().ToList();
    rows.Should().HaveCount(2);
    // Verify that type comes before member even though member has higher magnitude (60-25=35 vs 30-20=10)
    rows[0].GetProperty("symbolType").GetString().Should().Be("Type");
    rows[0].GetProperty("symbolFqn").GetString().Should().Be("Rca.Loader.Services.MixedType");
    rows[1].GetProperty("symbolType").GetString().Should().Be("Member");
    rows[1].GetProperty("symbolFqn").GetString().Should().Contain("Execute(...)");
  }

  [Test]
  public async Task ExecuteAsync_ThresholdOverride_IsApplied()
  {
    var report = MetricsReaderCommandTestData.CreateReport(new[]
    {
      MetricsReaderCommandTestData.CreateTypeNode("Rca.Loader.Services.Target", 12, ThresholdStatus.Warning)
    });

    var reportPath = WriteReport(report);
    var overridePath = WriteThresholdOverride(5, 6);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Loader.Services", thresholdsFile: overridePath);

    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ReadAnyCommand>(settings)
      .ConfigureAwait(false);

    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.GetProperty("threshold").GetDecimal().Should().Be(5);
    json.RootElement.GetProperty("thresholdKind").GetString().Should().Be("Warning");
  }

  [Test]
  public async Task ExecuteAsync_ShowAllTrue_EmptyNamespace_PrintsMessage()
  {
    var report = MetricsReaderCommandTestData.CreateReport(new[]
    {
      MetricsReaderCommandTestData.CreateTypeNode("Rca.Loader.Services.SomeType", 40, ThresholdStatus.Error)
    });

    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Other.Namespace", showAll: true);

    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ReadAnyCommand>(settings)
      .ConfigureAwait(false);

    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.GetProperty("metric").GetString().Should().Be("Complexity");
    json.RootElement.GetProperty("namespace").GetString().Should().Be("Rca.Other.Namespace");
    json.RootElement.GetProperty("message").GetString().Should().Contain("No violations were found");
  }

  [Test]
  public async Task ExecuteAsync_NoViolations_PrintsMessage()
  {
    var report = MetricsReaderCommandTestData.CreateReport(new[]
    {
      MetricsReaderCommandTestData.CreateTypeNode("Rca.Loader.Services.Clean", 5, ThresholdStatus.Success)
    });

    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Loader.Services");

    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ReadAnyCommand>(settings)
      .ConfigureAwait(false);

    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.GetProperty("metric").GetString().Should().Be("Complexity");
    json.RootElement.GetProperty("namespace").GetString().Should().Be("Rca.Loader.Services");
    json.RootElement.GetProperty("message").GetString().Should().Contain("No violations were found");
  }
}


