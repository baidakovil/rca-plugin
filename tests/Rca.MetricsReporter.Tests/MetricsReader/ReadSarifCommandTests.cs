namespace Rca.MetricsReporter.Tests.MetricsReader;

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Rca.Tools.MetricsReporter.MetricsReader.Commands;
using Rca.Tools.MetricsReporter.MetricsReader.Settings;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Tests for the readsarif command.
/// </summary>
[TestFixture]
[Category("Unit")]
[Parallelizable(ParallelScope.None)]
internal sealed class ReadSarifCommandTests : MetricsReaderCommandTestsBase
{
  [Test]
  public async Task ExecuteAsync_ReturnsLargestViolationGroupByDefault()
  {
    var type = MetricsReaderCommandTestData.CreateTypeNode(
      "Rca.Loader.Services.RuleConsumer",
      new Dictionary<MetricIdentifier, MetricValue>
      {
        [MetricIdentifier.SarifIdeRuleViolations] = CreateSarifMetric(
          ("IDE0060", new[]
          {
            ("Remove unused parameter", "file:///src/Consumer.cs", 42),
            ("Remove unused parameter", "file:///src/Consumer.cs", 48)
          }),
          ("IDE0040", new[]
          {
            ("Add call to ConfigureAwait", "file:///src/Consumer.cs", 30)
          }))
      });

    var report = MetricsReaderCommandTestData.CreateReport(new[] { type });
    report.Metadata.RuleDescriptions["IDE0060"] = new RuleDescription { ShortDescription = "Unused parameter" };
    report.Metadata.RuleDescriptions["IDE0040"] = new RuleDescription { ShortDescription = "Await configure" };

    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Loader.Services", metricName: "SarifIdeRuleViolations");

    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ReadSarifCommand>(settings)
      .ConfigureAwait(false);

    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    var root = json.RootElement;
    root.GetProperty("metric").GetString().Should().Be("SarifIdeRuleViolations");
    var groups = root.GetProperty("violationsGroups").EnumerateArray().ToList();
    groups.Should().HaveCount(1);
    var group = groups[0];
    group.GetProperty("ruleId").GetString().Should().Be("IDE0060");
    group.GetProperty("count").GetInt32().Should().Be(2);
    group.GetProperty("shortDescription").GetString().Should().Be("Unused parameter");
  }

  [Test]
  public async Task ExecuteAsync_AllFlag_ReturnsAllGroupsSortedByCount()
  {
    var type = MetricsReaderCommandTestData.CreateTypeNode(
      "Rca.Loader.Services.RuleConsumer",
      new Dictionary<MetricIdentifier, MetricValue>
      {
        [MetricIdentifier.SarifIdeRuleViolations] = CreateSarifMetric(
          ("IDE0060", new[]
          {
            ("Remove unused parameter", "file:///src/Consumer.cs", 42)
          }),
          ("IDE0040", new[]
          {
            ("Add call to ConfigureAwait", "file:///src/Consumer.cs", 30),
            ("Add call to ConfigureAwait", "file:///src/Consumer.cs", 31),
          }))
      });

    var report = MetricsReaderCommandTestData.CreateReport(new[] { type });
    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(
      reportPath,
      "Rca.Loader.Services",
      showAll: true,
      metricName: "SarifIdeRuleViolations");

    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ReadSarifCommand>(settings)
      .ConfigureAwait(false);

    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    var groups = json.RootElement.GetProperty("violationsGroups").EnumerateArray().ToList();
    groups.Should().HaveCount(2);
    groups[0].GetProperty("ruleId").GetString().Should().Be("IDE0040");
    groups[0].GetProperty("count").GetInt32().Should().Be(2);
    groups[1].GetProperty("ruleId").GetString().Should().Be("IDE0060");
    groups[1].GetProperty("count").GetInt32().Should().Be(1);
  }

  [Test]
  public async Task ExecuteAsync_NonSarifMetric_PrintsMessage()
  {
    var report = MetricsReaderCommandTestData.CreateReport(new[]
    {
      MetricsReaderCommandTestData.CreateTypeNode("Rca.Loader.Services.Target", 12, ThresholdStatus.Warning)
    });

    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Loader.Services", metricName: "Complexity");

    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ReadSarifCommand>(settings)
      .ConfigureAwait(false);

    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.GetProperty("metric").GetString().Should().Be("Complexity");
    json.RootElement.GetProperty("message").GetString().Should().Contain("does not expose SARIF rule breakdown data");
  }

  [Test]
  public async Task ExecuteAsync_NoMatchingViolations_PrintsMessage()
  {
    var type = MetricsReaderCommandTestData.CreateTypeNode(
      "Rca.Loader.Services.RuleConsumer",
      new Dictionary<MetricIdentifier, MetricValue>
      {
        [MetricIdentifier.SarifIdeRuleViolations] = CreateSarifMetric(
          ("IDE0060", new[]
          {
            ("Remove unused parameter", "file:///src/Consumer.cs", 42)
          }))
      });

    var report = MetricsReaderCommandTestData.CreateReport(new[] { type });
    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(
      reportPath,
      "Rca.Other.Namespace",
      metricName: "SarifIdeRuleViolations",
      showAll: true);

    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ReadSarifCommand>(settings)
      .ConfigureAwait(false);

    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.GetProperty("metric").GetString().Should().Be("SarifIdeRuleViolations");
    json.RootElement.GetProperty("namespace").GetString().Should().Be("Rca.Other.Namespace");
    json.RootElement.GetProperty("message").GetString().Should().Contain("No SARIF violations");
  }

  [Test]
  public async Task ExecuteAsync_MemberSymbolKindWithoutMemberMetrics_PrintsMessage()
  {
    var type = MetricsReaderCommandTestData.CreateTypeNode(
      "Rca.Loader.Services.RuleConsumer",
      new Dictionary<MetricIdentifier, MetricValue>
      {
        [MetricIdentifier.SarifCaRuleViolations] = CreateSarifMetric(
          ("CA1502", new[]
          {
            ("Avoid complexity", "file:///src/Consumer.cs", 10)
          }))
      });

    var report = MetricsReaderCommandTestData.CreateReport(new[] { type });
    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(
      reportPath,
      "Rca.Loader.Services",
      symbolKind: MetricsReaderSymbolKind.Member,
      metricName: "SarifCaRuleViolations",
      showAll: true);

    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ReadSarifCommand>(settings)
      .ConfigureAwait(false);

    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.GetProperty("metric").GetString().Should().Be("SarifCaRuleViolations");
    json.RootElement.GetProperty("symbolKind").GetString().Should().Be("Member");
    json.RootElement.GetProperty("message").GetString().Should().Contain("No SARIF violations");
  }

  [Test]
  public async Task ExecuteAsync_WithRuleIdFilter_ReturnsOnlyMatchingGroup()
  {
    var type = MetricsReaderCommandTestData.CreateTypeNode(
      "Rca.Loader.Services.RuleConsumer",
      new Dictionary<MetricIdentifier, MetricValue>
      {
        [MetricIdentifier.SarifCaRuleViolations] = CreateSarifMetric(
          ("CA1502", new[]
          {
            ("Avoid complexity", "file:///src/Consumer.cs", 10)
          }),
          ("CA1506", new[]
          {
            ("Reduce coupling", "file:///src/Consumer.cs", 20),
            ("Reduce coupling", "file:///src/Consumer.cs", 25)
          }))
      });

    var report = MetricsReaderCommandTestData.CreateReport(new[] { type });
    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(
      reportPath,
      "Rca.Loader.Services",
      showAll: true,
      metricName: "SarifCaRuleViolations",
      ruleId: "CA1506");

    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ReadSarifCommand>(settings)
      .ConfigureAwait(false);

    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    var groups = json.RootElement.GetProperty("violationsGroups").EnumerateArray().ToList();
    groups.Should().HaveCount(1);
    groups[0].GetProperty("ruleId").GetString().Should().Be("CA1506");
    groups[0].GetProperty("count").GetInt32().Should().Be(2);
  }

  [Test]
  public async Task ExecuteAsync_WithRuleIdFilter_IsCaseInsensitive()
  {
    var type = MetricsReaderCommandTestData.CreateTypeNode(
      "Rca.Loader.Services.RuleConsumer",
      new Dictionary<MetricIdentifier, MetricValue>
      {
        [MetricIdentifier.SarifIdeRuleViolations] = CreateSarifMetric(
          ("IDE0060", new[]
          {
            ("Remove unused parameter", "file:///src/Consumer.cs", 42)
          }))
      });

    var report = MetricsReaderCommandTestData.CreateReport(new[] { type });
    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(
      reportPath,
      "Rca.Loader.Services",
      showAll: true,
      metricName: "SarifIdeRuleViolations",
      ruleId: "ide0060");

    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ReadSarifCommand>(settings)
      .ConfigureAwait(false);

    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    var groups = json.RootElement.GetProperty("violationsGroups").EnumerateArray().ToList();
    groups.Should().HaveCount(1);
    groups[0].GetProperty("ruleId").GetString().Should().Be("IDE0060");
  }

  [Test]
  public async Task ExecuteAsync_WithRuleIdFilterButNoMatches_PrintsMessage()
  {
    var type = MetricsReaderCommandTestData.CreateTypeNode(
      "Rca.Loader.Services.RuleConsumer",
      new Dictionary<MetricIdentifier, MetricValue>
      {
        [MetricIdentifier.SarifCaRuleViolations] = CreateSarifMetric(
          ("CA1502", new[]
          {
            ("Avoid complexity", "file:///src/Consumer.cs", 10)
          }))
      });

    var report = MetricsReaderCommandTestData.CreateReport(new[] { type });
    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(
      reportPath,
      "Rca.Loader.Services",
      metricName: "SarifCaRuleViolations",
      ruleId: "CA9999");

    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ReadSarifCommand>(settings)
      .ConfigureAwait(false);

    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.GetProperty("metric").GetString().Should().Be("SarifCaRuleViolations");
    json.RootElement.GetProperty("message").GetString().Should().Contain("No SARIF violations");
  }

  [Test]
  public async Task ExecuteAsync_DefaultSymbolKindAny_IncludesMemberViolations()
  {
    var member = MetricsReaderCommandTestData.CreateMemberNode(
      "Rca.Loader.Services.RuleConsumer.Process(...)",
      new Dictionary<MetricIdentifier, MetricValue>
      {
        [MetricIdentifier.SarifCaRuleViolations] = CreateSarifMetric(("CA1502", new[]
        {
          ("Avoid complexity", "file:///src/Consumer.cs", 10)
        }))
      });

    var type = MetricsReaderCommandTestData.CreateTypeNode(
      "Rca.Loader.Services.RuleConsumer",
      new Dictionary<MetricIdentifier, MetricValue>(),
      new[] { member });

    var report = MetricsReaderCommandTestData.CreateReport(new[] { type });
    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(
      reportPath,
      "Rca.Loader.Services",
      showAll: true,
      metricName: "SarifCaRuleViolations");

    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ReadSarifCommand>(settings)
      .ConfigureAwait(false);

    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    var group = json.RootElement.GetProperty("violationsGroups")[0];
    group.GetProperty("ruleId").GetString().Should().Be("CA1502");
    group.GetProperty("violations")[0].GetProperty("symbol").GetString().Should().Contain("Process(...)");
  }

  [Test]
  public async Task ExecuteAsync_SuppressedSymbolsExcludedByDefault()
  {
    const string suppressedFqn = "Rca.Loader.Services.RuleConsumer";
    var type = MetricsReaderCommandTestData.CreateTypeNode(
      suppressedFqn,
      new Dictionary<MetricIdentifier, MetricValue>
      {
        [MetricIdentifier.SarifCaRuleViolations] = CreateSarifMetric(
          ("CA1502", new[]
          {
            ("Avoid complexity", "file:///src/Consumer.cs", 10)
          }))
      });

    var suppressedInfo = new SuppressedSymbolInfo
    {
      FullyQualifiedName = suppressedFqn,
      Metric = MetricIdentifier.SarifCaRuleViolations.ToString(),
      RuleId = "CA1502",
      FilePath = "src/Rca.Loader/RuleConsumer.cs"
    };

    var report = MetricsReaderCommandTestData.CreateReport(new[] { type }, new[] { suppressedInfo });
    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Loader.Services", metricName: "SarifCaRuleViolations");

    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ReadSarifCommand>(settings)
      .ConfigureAwait(false);

    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.GetProperty("metric").GetString().Should().Be("SarifCaRuleViolations");
    json.RootElement.GetProperty("message").GetString().Should().Contain("No SARIF violations");
  }

  [Test]
  public async Task ExecuteAsync_SuppressedSymbolsIncludedWhenRequested()
  {
    const string suppressedFqn = "Rca.Loader.Services.RuleConsumer";
    var type = MetricsReaderCommandTestData.CreateTypeNode(
      suppressedFqn,
      new Dictionary<MetricIdentifier, MetricValue>
      {
        [MetricIdentifier.SarifCaRuleViolations] = CreateSarifMetric(
          ("CA1502", new[]
          {
            ("Avoid complexity", "file:///src/Consumer.cs", 10)
          }))
      });

    var suppressedInfo = new SuppressedSymbolInfo
    {
      FullyQualifiedName = suppressedFqn,
      Metric = MetricIdentifier.SarifCaRuleViolations.ToString(),
      RuleId = "CA1502",
      FilePath = "src/Rca.Loader/RuleConsumer.cs"
    };

    var report = MetricsReaderCommandTestData.CreateReport(new[] { type }, new[] { suppressedInfo });
    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(
      reportPath,
      "Rca.Loader.Services",
      includeSuppressed: true,
      showAll: true,
      metricName: "SarifCaRuleViolations");

    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ReadSarifCommand>(settings)
      .ConfigureAwait(false);

    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    var groups = json.RootElement.GetProperty("violationsGroups").EnumerateArray().ToList();
    groups.Should().HaveCount(1);
    groups[0].GetProperty("ruleId").GetString().Should().Be("CA1502");
  }

  [Test]
  public async Task ExecuteAsync_SuppressedViaRuleIdMapping_ExcludedByDefault()
  {
    const string suppressedFqn = "Rca.Loader.Services.RuleConsumer";
    var type = MetricsReaderCommandTestData.CreateTypeNode(
      suppressedFqn,
      new Dictionary<MetricIdentifier, MetricValue>
      {
        [MetricIdentifier.SarifCaRuleViolations] = CreateSarifMetric(
          ("CA1506", new[]
          {
            ("Avoid excessive class coupling", "file:///src/Consumer.cs", 10)
          }))
      });

    var suppressedInfo = new SuppressedSymbolInfo
    {
      FullyQualifiedName = suppressedFqn,
      Metric = MetricIdentifier.RoslynClassCoupling.ToString(),
      RuleId = "CA1506",
      FilePath = "src/Rca.Loader/RuleConsumer.cs"
    };

    var report = MetricsReaderCommandTestData.CreateReport(new[] { type }, new[] { suppressedInfo });
    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(reportPath, "Rca.Loader.Services", metricName: "SarifCaRuleViolations");

    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ReadSarifCommand>(settings)
      .ConfigureAwait(false);

    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    json.RootElement.GetProperty("metric").GetString().Should().Be("SarifCaRuleViolations");
    json.RootElement.GetProperty("message").GetString().Should().Contain("No SARIF violations");
  }

  [Test]
  public async Task ExecuteAsync_MemberSymbolKind_UsesMemberSymbols()
  {
    var memberMetric = CreateSarifMetric(("IDE0060", new[]
    {
      ("Remove unused parameter", "file:///src/Member.cs", 10)
    }));

    var member = MetricsReaderCommandTestData.CreateMemberNode(
      "Rca.Loader.Services.Type.DoWork(...)",
      new Dictionary<MetricIdentifier, MetricValue>
      {
        [MetricIdentifier.SarifIdeRuleViolations] = memberMetric
      });

    var type = MetricsReaderCommandTestData.CreateTypeNode(
      "Rca.Loader.Services.Type",
      new Dictionary<MetricIdentifier, MetricValue>(),
      new[] { member });

    var report = MetricsReaderCommandTestData.CreateReport(new[] { type });
    var reportPath = WriteReport(report);
    var settings = CreateNamespaceSettings(
      reportPath,
      "Rca.Loader.Services",
      symbolKind: MetricsReaderSymbolKind.Member,
      showAll: true,
      metricName: "SarifIdeRuleViolations");

    var (exitCode, output) = await MetricsReaderCommandTestHarness
      .RunNamespaceCommandAsync<ReadSarifCommand>(settings)
      .ConfigureAwait(false);

    exitCode.Should().Be(0);
    using var json = JsonDocument.Parse(output);
    var violation = json.RootElement
      .GetProperty("violationsGroups")[0]
      .GetProperty("violations")[0];

    violation.GetProperty("symbol").GetString().Should().Be("Rca.Loader.Services.Type.DoWork(...)");
  }

  private static MetricValue CreateSarifMetric(params (string RuleId, (string Message, string Uri, int StartLine)[] Violations)[] entries)
  {
    var breakdown = new Dictionary<string, SarifRuleBreakdownEntry>();
    foreach (var entry in entries)
    {
      var violations = entry.Violations
        .Select(detail => new SarifRuleViolationDetail
        {
          Message = detail.Message,
          Uri = detail.Uri,
          StartLine = detail.StartLine,
          EndLine = detail.StartLine
        })
        .ToList();

      breakdown[entry.RuleId] = new SarifRuleBreakdownEntry
      {
        Count = violations.Count,
        Violations = violations
      };
    }

    return new MetricValue
    {
      Value = breakdown.Values.Sum(v => (decimal?)v.Count),
      Status = ThresholdStatus.NotApplicable,
      Breakdown = breakdown
    };
  }
}


