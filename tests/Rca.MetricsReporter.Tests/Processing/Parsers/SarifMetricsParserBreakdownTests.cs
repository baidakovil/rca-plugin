namespace Rca.MetricsReporter.Tests.Processing.Parsers;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Rca.Tools.MetricsReporter.Model;
using Rca.Tools.MetricsReporter.Processing.Parsers;

/// <summary>
/// Unit tests for <see cref="SarifMetricsParser"/> focusing on breakdown functionality.
/// </summary>
[TestFixture]
[Category("Unit")]
public sealed class SarifMetricsParserBreakdownTests
{
  private SarifMetricsParser parser = null!;

  [SetUp]
  public void SetUp()
  {
    parser = new SarifMetricsParser();
  }

  [Test]
  public async Task ParseAsync_SingleCARule_CreatesBreakdown()
  {
    // Arrange
    var sarif = CreateSarifJson(
        ruleId: "CA1502",
        filePath: "file:///C:/Repo/Sample.cs",
        startLine: 10);

    var tempFile = CreateTempFile(sarif);

    try
    {
      // Act
      var result = await parser.ParseAsync(tempFile, CancellationToken.None);

      // Assert
      result.Elements.Should().HaveCount(1);
      var element = result.Elements.First();
      element.Metrics.Should().ContainKey(MetricIdentifier.SarifCaRuleViolations);
      
      var metric = element.Metrics[MetricIdentifier.SarifCaRuleViolations];
      metric.Value.Should().Be(1);
      metric.Breakdown.Should().NotBeNull().And.ContainKey("CA1502");
      metric.Breakdown!["CA1502"].Should().Be(1);
    }
    finally
    {
      File.Delete(tempFile);
    }
  }

  [Test]
  public async Task ParseAsync_SingleIDERule_CreatesBreakdown()
  {
    // Arrange
    var sarif = CreateSarifJson(
        ruleId: "IDE0051",
        filePath: "file:///C:/Repo/Sample.cs",
        startLine: 15);

    var tempFile = CreateTempFile(sarif);

    try
    {
      // Act
      var result = await parser.ParseAsync(tempFile, CancellationToken.None);

      // Assert
      result.Elements.Should().HaveCount(1);
      var element = result.Elements.First();
      element.Metrics.Should().ContainKey(MetricIdentifier.SarifIdeRuleViolations);
      
      var metric = element.Metrics[MetricIdentifier.SarifIdeRuleViolations];
      metric.Value.Should().Be(1);
      metric.Breakdown.Should().NotBeNull().And.ContainKey("IDE0051");
      metric.Breakdown!["IDE0051"].Should().Be(1);
    }
    finally
    {
      File.Delete(tempFile);
    }
  }

  [Test]
  public async Task ParseAsync_MultipleSameRule_MultipleLocations_CreatesMultipleElements()
  {
    // Arrange - Same rule, different locations
    var sarif = CreateSarifJsonWithMultipleResults(
        new[] { ("CA1502", "file:///C:/Repo/Sample1.cs", 10), ("CA1502", "file:///C:/Repo/Sample2.cs", 20) });

    var tempFile = CreateTempFile(sarif);

    try
    {
      // Act
      var result = await parser.ParseAsync(tempFile, CancellationToken.None);

      // Assert
      result.Elements.Should().HaveCount(2);
      foreach (var element in result.Elements)
      {
        var metric = element.Metrics[MetricIdentifier.SarifCaRuleViolations];
        metric.Value.Should().Be(1);
        metric.Breakdown.Should().NotBeNull().And.ContainKey("CA1502");
        metric.Breakdown!["CA1502"].Should().Be(1);
      }
    }
    finally
    {
      File.Delete(tempFile);
    }
  }

  [Test]
  public async Task ParseAsync_InvalidRuleId_NoBreakdown()
  {
    // Arrange - Invalid rule ID (not CA#### or IDE####)
    var sarif = CreateSarifJson(
        ruleId: "SYSLIB1045", // Not a CA or IDE rule
        filePath: "file:///C:/Repo/Sample.cs",
        startLine: 10);

    var tempFile = CreateTempFile(sarif);

    try
    {
      // Act
      var result = await parser.ParseAsync(tempFile, CancellationToken.None);

      // Assert
      result.Elements.Should().BeEmpty("Invalid rule IDs should be filtered out");
    }
    finally
    {
      File.Delete(tempFile);
    }
  }

  [Test]
  public async Task ParseAsync_MalformedRuleId_NoBreakdown()
  {
    // Arrange - Malformed rule ID (CA123 is too short, but parser still processes it)
    // The parser doesn't validate rule ID format, it only checks if it starts with CA or IDE
    // Validation happens in RuleIdValidator, which is used when creating breakdown
    var sarif = CreateSarifJson(
        ruleId: "CA123", // Too short, but parser will still process it
        filePath: "file:///C:/Repo/Sample.cs",
        startLine: 10);

    var tempFile = CreateTempFile(sarif);

    try
    {
      // Act
      var result = await parser.ParseAsync(tempFile, CancellationToken.None);

      // Assert
      // Parser processes it, but breakdown will be null because RuleIdValidator rejects it
      result.Elements.Should().HaveCount(1);
      var element = result.Elements.First();
      element.Metrics.Should().ContainKey(MetricIdentifier.SarifCaRuleViolations);
      var metric = element.Metrics[MetricIdentifier.SarifCaRuleViolations];
      metric.Breakdown.Should().BeNull("Malformed rule IDs should not create breakdown");
    }
    finally
    {
      File.Delete(tempFile);
    }
  }

  [Test]
  public async Task ParseAsync_RealWorldMultipleRules_CreatesCorrectBreakdown()
  {
    // Arrange - Real-world scenario with multiple different rules
    var sarif = CreateSarifJsonWithMultipleResults(
        new[]
        {
          ("CA1502", "file:///C:/Repo/Sample.cs", 10),
          ("CA1506", "file:///C:/Repo/Sample.cs", 15),
          ("CA1502", "file:///C:/Repo/Sample.cs", 20), // Duplicate CA1502
          ("IDE0051", "file:///C:/Repo/Sample.cs", 25),
          ("IDE0028", "file:///C:/Repo/Sample.cs", 30),
        });

    var tempFile = CreateTempFile(sarif);

    try
    {
      // Act
      var result = await parser.ParseAsync(tempFile, CancellationToken.None);

      // Assert
      result.Elements.Should().HaveCount(5);
      
      // Check CA rules
      var caElements = result.Elements.Where(e => e.Metrics.ContainsKey(MetricIdentifier.SarifCaRuleViolations)).ToList();
      caElements.Should().HaveCount(3);
      
      // Check IDE rules
      var ideElements = result.Elements.Where(e => e.Metrics.ContainsKey(MetricIdentifier.SarifIdeRuleViolations)).ToList();
      ideElements.Should().HaveCount(2);
      
      // Verify breakdowns
      foreach (var element in caElements)
      {
        var breakdown = element.Metrics[MetricIdentifier.SarifCaRuleViolations].Breakdown;
        breakdown.Should().NotBeNull();
        breakdown!.Count.Should().Be(1);
      }
      
      foreach (var element in ideElements)
      {
        var breakdown = element.Metrics[MetricIdentifier.SarifIdeRuleViolations].Breakdown;
        breakdown.Should().NotBeNull();
        breakdown!.Count.Should().Be(1);
      }
    }
    finally
    {
      File.Delete(tempFile);
    }
  }

  [Test]
  public async Task ParseAsync_MultipleLocationsInSingleResult_CreatesMultipleElements()
  {
    // Arrange - Single result with multiple locations
    var sarif = @"{
  ""$schema"": ""http://json.schemastore.org/sarif-2.1.0"",
  ""version"": ""2.1.0"",
  ""runs"": [
    {
      ""results"": [
        {
          ""ruleId"": ""CA1502"",
          ""level"": ""warning"",
          ""locations"": [
            {
              ""physicalLocation"": {
                ""artifactLocation"": {
                  ""uri"": ""file:///C:/Repo/Sample.cs""
                },
                ""region"": {
                  ""startLine"": 10
                }
              }
            },
            {
              ""physicalLocation"": {
                ""artifactLocation"": {
                  ""uri"": ""file:///C:/Repo/Sample.cs""
                },
                ""region"": {
                  ""startLine"": 20
                }
              }
            }
          ]
        }
      ]
    }
  ]
}";

    var tempFile = CreateTempFile(sarif);

    try
    {
      // Act
      var result = await parser.ParseAsync(tempFile, CancellationToken.None);

      // Assert
      result.Elements.Should().HaveCount(2);
      foreach (var element in result.Elements)
      {
        var metric = element.Metrics[MetricIdentifier.SarifCaRuleViolations];
        metric.Value.Should().Be(1);
        metric.Breakdown.Should().NotBeNull().And.ContainKey("CA1502");
        metric.Breakdown!["CA1502"].Should().Be(1);
      }
    }
    finally
    {
      File.Delete(tempFile);
    }
  }

  [Test]
  public async Task ParseAsync_EmptyRuns_ReturnsEmptyDocument()
  {
    // Arrange
    var sarif = @"{
  ""$schema"": ""http://json.schemastore.org/sarif-2.1.0"",
  ""version"": ""2.1.0"",
  ""runs"": []
}";

    var tempFile = CreateTempFile(sarif);

    try
    {
      // Act
      var result = await parser.ParseAsync(tempFile, CancellationToken.None);

      // Assert
      result.Elements.Should().BeEmpty();
    }
    finally
    {
      File.Delete(tempFile);
    }
  }

  [Test]
  public async Task ParseAsync_NoResults_ReturnsEmptyDocument()
  {
    // Arrange
    var sarif = @"{
  ""$schema"": ""http://json.schemastore.org/sarif-2.1.0"",
  ""version"": ""2.1.0"",
  ""runs"": [
    {
      ""results"": []
    }
  ]
}";

    var tempFile = CreateTempFile(sarif);

    try
    {
      // Act
      var result = await parser.ParseAsync(tempFile, CancellationToken.None);

      // Assert
      result.Elements.Should().BeEmpty();
    }
    finally
    {
      File.Delete(tempFile);
    }
  }

  [Test]
  public async Task ParseAsync_MissingRuleId_SkipsResult()
  {
    // Arrange
    var sarif = @"{
  ""$schema"": ""http://json.schemastore.org/sarif-2.1.0"",
  ""version"": ""2.1.0"",
  ""runs"": [
    {
      ""results"": [
        {
          ""level"": ""warning"",
          ""locations"": [
            {
              ""physicalLocation"": {
                ""artifactLocation"": {
                  ""uri"": ""file:///C:/Repo/Sample.cs""
                },
                ""region"": {
                  ""startLine"": 10
                }
              }
            }
          ]
        }
      ]
    }
  ]
}";

    var tempFile = CreateTempFile(sarif);

    try
    {
      // Act
      var result = await parser.ParseAsync(tempFile, CancellationToken.None);

      // Assert
      result.Elements.Should().BeEmpty("Results without ruleId should be skipped");
    }
    finally
    {
      File.Delete(tempFile);
    }
  }

  [Test]
  public async Task ParseAsync_MissingLocation_SkipsResult()
  {
    // Arrange
    var sarif = @"{
  ""$schema"": ""http://json.schemastore.org/sarif-2.1.0"",
  ""version"": ""2.1.0"",
  ""runs"": [
    {
      ""results"": [
        {
          ""ruleId"": ""CA1502"",
          ""level"": ""warning""
        }
      ]
    }
  ]
}";

    var tempFile = CreateTempFile(sarif);

    try
    {
      // Act
      var result = await parser.ParseAsync(tempFile, CancellationToken.None);

      // Assert
      result.Elements.Should().BeEmpty("Results without locations should be skipped");
    }
    finally
    {
      File.Delete(tempFile);
    }
  }

  private static string CreateSarifJson(string ruleId, string filePath, int startLine)
  {
    return $@"{{
  ""$schema"": ""http://json.schemastore.org/sarif-2.1.0"",
  ""version"": ""2.1.0"",
  ""runs"": [
    {{
      ""results"": [
        {{
          ""ruleId"": ""{ruleId}"",
          ""ruleIndex"": 0,
          ""level"": ""warning"",
          ""message"": {{
            ""text"": ""Test message""
          }},
          ""locations"": [
            {{
              ""physicalLocation"": {{
                ""artifactLocation"": {{
                  ""uri"": ""{filePath}""
                }},
                ""region"": {{
                  ""startLine"": {startLine},
                  ""startColumn"": 1,
                  ""endLine"": {startLine},
                  ""endColumn"": 10
                }}
              }}
            }}
          ],
          ""properties"": {{
            ""warningLevel"": 1
          }}
        }}
      ]
    }}
  ]
}}";
  }

  private static string CreateSarifJsonWithMultipleResults((string ruleId, string filePath, int startLine)[] results)
  {
    var resultsJson = string.Join(",\n        ", results.Select((r, i) => $@"{{
          ""ruleId"": ""{r.ruleId}"",
          ""ruleIndex"": {i},
          ""level"": ""warning"",
          ""message"": {{
            ""text"": ""Test message for {r.ruleId}""
          }},
          ""locations"": [
            {{
              ""physicalLocation"": {{
                ""artifactLocation"": {{
                  ""uri"": ""{r.filePath}""
                }},
                ""region"": {{
                  ""startLine"": {r.startLine},
                  ""startColumn"": 1,
                  ""endLine"": {r.startLine},
                  ""endColumn"": 10
                }}
              }}
            }}
          ],
          ""properties"": {{
            ""warningLevel"": 1
          }}
        }}"));

    return $@"{{
  ""$schema"": ""http://json.schemastore.org/sarif-2.1.0"",
  ""version"": ""2.1.0"",
  ""runs"": [
    {{
      ""results"": [
        {resultsJson}
      ]
    }}
  ]
}}";
  }

  private static string CreateTempFile(string content)
  {
    var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".sarif");
    File.WriteAllText(tempFile, content);
    return tempFile;
  }
}

