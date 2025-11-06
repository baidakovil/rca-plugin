namespace Rca.MetricsReporter.Tests.Configuration;

using FluentAssertions;
using NUnit.Framework;
using Rca.Tools.MetricsReporter.Configuration;
using Rca.Tools.MetricsReporter.Model;

[TestFixture]
[Category("Unit")]
public sealed class ThresholdsParserTests
{
    private ThresholdsParser parser = null!;

    [SetUp]
    public void SetUp()
    {
        parser = new ThresholdsParser();
    }

    [Test]
    public void Parse_NullInput_ProducesDefaultThresholds()
    {
        // Act
        var result = parser.Parse(null);

        // Assert
        result.Should().HaveCount(Enum.GetValues<MetricIdentifier>().Length);
        result[MetricIdentifier.AltCoverSequenceCoverage].HigherIsBetter.Should().BeTrue();
        result[MetricIdentifier.SarifCaRuleViolations].HigherIsBetter.Should().BeFalse();
    }

    [Test]
    public void Parse_CustomOverrides_UpdatesSpecifiedMetrics()
    {
        // Arrange
        const string customJson = "{'AltCoverSequenceCoverage':{'warning':80,'error':70,'higherIsBetter':true},'SarifCaRuleViolations':{'warning':1,'error':2,'higherIsBetter':false}}";

        // Act
        var result = parser.Parse(customJson);

        // Assert
        result[MetricIdentifier.AltCoverSequenceCoverage].Warning.Should().Be(80);
        result[MetricIdentifier.AltCoverSequenceCoverage].Error.Should().Be(70);
        result[MetricIdentifier.SarifCaRuleViolations].Warning.Should().Be(1);
        result[MetricIdentifier.SarifCaRuleViolations].Error.Should().Be(2);
    }

    [Test]
    public void Parse_InvalidJson_ThrowsInvalidOperationException()
    {
        // Arrange
        const string invalidJson = "{'AltCoverSequenceCoverage':{'warning':}";

        // Act
        var act = () => parser.Parse(invalidJson);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}

