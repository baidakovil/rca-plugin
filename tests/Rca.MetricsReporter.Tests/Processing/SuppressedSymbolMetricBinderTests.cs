namespace Rca.MetricsReporter.Tests.Processing;

using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using Rca.Tools.MetricsReporter.Aggregation;
using Rca.Tools.MetricsReporter.Model;

[TestFixture]
[Category("Unit")]
public sealed class SuppressedSymbolMetricBinderTests
{
  [Test]
  public void Bind_AssignsMetricBasedOnNodeMetric()
  {
    var solution = new SolutionMetricsNode
    {
      Name = "Root",
      FullyQualifiedName = "Root"
    };

    var assembly = new AssemblyMetricsNode
    {
      Name = "Sample.Assembly",
      FullyQualifiedName = "Sample.Assembly"
    };

    var ns = new NamespaceMetricsNode
    {
      Name = "Sample.Namespace",
      FullyQualifiedName = "Sample.Namespace"
    };

    var type = new TypeMetricsNode
    {
      Name = "SampleType",
      FullyQualifiedName = "Sample.Namespace.SampleType"
    };

    var member = new MemberMetricsNode
    {
      Name = "SuppressedProperty",
      FullyQualifiedName = "Sample.Namespace.SampleType.SuppressedProperties"
    };
    member.Metrics[MetricIdentifier.SarifIdeRuleViolations] = new MetricValue { Value = 1m };

    type.Members.Add(member);
    ns.Types.Add(type);
    assembly.Namespaces.Add(ns);
    solution.Assemblies.Add(assembly);

    var suppressed = new List<SuppressedSymbolInfo>
    {
      new()
      {
        FullyQualifiedName = "Sample.Namespace.SampleType.SuppressedProperties",
        RuleId = "IDE0028"
      }
    };

    SuppressedSymbolMetricBinder.Bind(solution, suppressed);

    suppressed[0].Metric.Should().Be(MetricIdentifier.SarifIdeRuleViolations.ToString());
  }
}

