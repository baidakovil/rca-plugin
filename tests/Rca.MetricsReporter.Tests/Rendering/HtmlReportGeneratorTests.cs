namespace Rca.MetricsReporter.Tests.Rendering;

using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using Rca.Tools.MetricsReporter.Model;
using Rca.Tools.MetricsReporter.Rendering;

[TestFixture]
[Category("Unit")]
public sealed class HtmlReportGeneratorTests
{
    [Test]
    public void Generate_BuildsHtmlWithStatusAndNewIndicators()
    {
        var member = new MemberMetricsNode
        {
            Name = "DoWork",
            FullyQualifiedName = "Sample.Namespace.SampleType.DoWork()",
            IsNew = true,
            Metrics = new Dictionary<MetricIdentifier, MetricValue>
            {
                [MetricIdentifier.RoslynMaintainabilityIndex] = new MetricValue
                {
                    Value = 40,
                    Status = ThresholdStatus.Error,
                    Unit = "score"
                }
            }
        };

        var type = new TypeMetricsNode
        {
            Name = "SampleType",
            FullyQualifiedName = "Sample.Namespace.SampleType",
            Metrics = new Dictionary<MetricIdentifier, MetricValue>(),
            Members = new List<MemberMetricsNode> { member }
        };

        var @namespace = new NamespaceMetricsNode
        {
            Name = "Sample.Namespace",
            FullyQualifiedName = "Sample.Namespace",
            Metrics = new Dictionary<MetricIdentifier, MetricValue>(),
            Types = new List<TypeMetricsNode> { type }
        };

        var assembly = new AssemblyMetricsNode
        {
            Name = "Sample.Assembly",
            FullyQualifiedName = "Sample.Assembly",
            Metrics = new Dictionary<MetricIdentifier, MetricValue>(),
            Namespaces = new List<NamespaceMetricsNode> { @namespace }
        };

        var report = new MetricsReport
        {
            Metadata = new ReportMetadata
            {
                GeneratedAtUtc = DateTime.UtcNow,
                Paths = new ReportPaths
                {
                    MetricsDirectory = @"C:\Repo\build\Metrics",
                    Report = @"C:\Repo\build\Metrics\Report\metrics-report.json",
                    Html = @"C:\Repo\build\Metrics\Report\metrics-report.html"
                },
                ThresholdsByLevel = new Dictionary<MetricIdentifier, IDictionary<MetricSymbolLevel, MetricThreshold>>(),
                ThresholdDescriptions = new Dictionary<MetricIdentifier, string?>()
            },
            Solution = new SolutionMetricsNode
            {
                Name = "SampleSolution",
                FullyQualifiedName = "SampleSolution",
                Metrics = new Dictionary<MetricIdentifier, MetricValue>(),
                Assemblies = new List<AssemblyMetricsNode> { assembly }
            }
        };

        var generator = new HtmlReportGenerator();

        var html = generator.Generate(report);

        html.Should().Contain("SampleSolution");
        html.Should().Contain("badge-new");
        html.Should().Contain("status-error");
        html.Should().Contain("DoWork");
    }
}

