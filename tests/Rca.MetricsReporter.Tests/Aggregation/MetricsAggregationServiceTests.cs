namespace Rca.MetricsReporter.Tests.Aggregation;

using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using Rca.Tools.MetricsReporter.Aggregation;
using Rca.Tools.MetricsReporter.Model;
using Rca.Tools.MetricsReporter.Processing;

[TestFixture]
[Category("Unit")]
public sealed class MetricsAggregationServiceTests
{
    private MetricsAggregationService service = null!;
    private Dictionary<MetricIdentifier, MetricThreshold> thresholds = null!;

    [SetUp]
    public void SetUp()
    {
        service = new MetricsAggregationService();
        thresholds = new Dictionary<MetricIdentifier, MetricThreshold>
        {
            [MetricIdentifier.RoslynMaintainabilityIndex] = new() { Warning = 65, Error = 40, HigherIsBetter = true },
            [MetricIdentifier.AltCoverSequenceCoverage] = new() { Warning = 70, Error = 50, HigherIsBetter = true },
            [MetricIdentifier.SarifCaRuleViolations] = new() { Warning = 1, Error = 2, HigherIsBetter = false }
        };
    }

    [Test]
    public void BuildReport_MergesSourcesAndCalculatesDeltas()
    {
        const string assemblyName = "Sample.Assembly";
        const string namespaceFqn = "Sample.Namespace";
        const string typeFqn = "Sample.Namespace.SampleType";
        const string memberFqn = "Sample.Namespace.SampleType.DoWork()";
        const string filePath = @"C:\Repo\Sample.cs";

        var roslynDocument = new ParsedMetricsDocument
        {
            SolutionName = "SampleSolution",
            Elements = new List<ParsedCodeElement>
            {
                new(CodeElementKind.Assembly, assemblyName, assemblyName)
                {
                    Metrics = new Dictionary<MetricIdentifier, MetricValue>
                    {
                        [MetricIdentifier.RoslynMaintainabilityIndex] = Metric(80, "score")
                    }
                },
                new(CodeElementKind.Namespace, namespaceFqn, namespaceFqn)
                {
                    ParentFullyQualifiedName = assemblyName
                },
                new(CodeElementKind.Type, "SampleType", typeFqn)
                {
                    ParentFullyQualifiedName = namespaceFqn,
                    Source = new SourceLocation { Path = filePath, StartLine = 5, EndLine = 20 }
                },
                new(CodeElementKind.Member, "DoWork", memberFqn)
                {
                    ParentFullyQualifiedName = typeFqn,
                    Source = new SourceLocation { Path = filePath, StartLine = 10, EndLine = 18 },
                    Metrics = new Dictionary<MetricIdentifier, MetricValue>
                    {
                        [MetricIdentifier.RoslynMaintainabilityIndex] = Metric(80, "score")
                    }
                },
                new(CodeElementKind.Member, "NewWork", $"{typeFqn}.NewWork()")
                {
                    ParentFullyQualifiedName = typeFqn,
                    Source = new SourceLocation { Path = filePath, StartLine = 30, EndLine = 35 },
                    Metrics = new Dictionary<MetricIdentifier, MetricValue>
                    {
                        [MetricIdentifier.RoslynMaintainabilityIndex] = Metric(55, "score")
                    }
                }
            }
        };

        var altCoverDocument = new ParsedMetricsDocument
        {
            Elements = new List<ParsedCodeElement>
            {
                new(CodeElementKind.Assembly, assemblyName, assemblyName),
                new(CodeElementKind.Type, "Sample.Namespace.SampleType", typeFqn)
                {
                    ParentFullyQualifiedName = assemblyName,
                    Metrics = new Dictionary<MetricIdentifier, MetricValue>()
                },
                new(CodeElementKind.Member, "Sample.Namespace.SampleType::DoWork()", memberFqn)
                {
                    ParentFullyQualifiedName = typeFqn,
                    Source = new SourceLocation { Path = filePath, StartLine = 10, EndLine = 18 },
                    Metrics = new Dictionary<MetricIdentifier, MetricValue>
                    {
                        [MetricIdentifier.AltCoverSequenceCoverage] = Metric(95, "percent")
                    }
                }
            }
        };

        var sarifDocument = new ParsedMetricsDocument
        {
            Elements = new List<ParsedCodeElement>
            {
                new(CodeElementKind.Member, "CA1000", null)
                {
                    Source = new SourceLocation { Path = filePath, StartLine = 12, EndLine = 12 },
                    Metrics = new Dictionary<MetricIdentifier, MetricValue>
                    {
                        [MetricIdentifier.SarifCaRuleViolations] = Metric(1, "count")
                    }
                }
            }
        };

        var baselineReport = CreateBaselineReport(assemblyName, namespaceFqn, typeFqn, memberFqn, 75);

        var input = new MetricsAggregationInput
        {
            SolutionName = "SampleSolution",
            AltCoverDocuments = new List<ParsedMetricsDocument> { altCoverDocument },
            RoslynDocuments = new List<ParsedMetricsDocument> { roslynDocument },
            SarifDocuments = new List<ParsedMetricsDocument> { sarifDocument },
            Baseline = baselineReport,
            Thresholds = thresholds,
            Paths = new ReportPaths
            {
                MetricsDirectory = @"C:\Repo\build\Metrics",
                Baseline = @"C:\Repo\build\Metrics\Report\metrics-baseline.json",
                Report = @"C:\Repo\build\Metrics\Report\metrics-report.json",
                Html = @"C:\Repo\build\Metrics\Report\metrics-report.html"
            }
        };

        var report = service.BuildReport(input);

        var assembly = report.Solution.Assemblies.Should().ContainSingle(a => a.Name == assemblyName).Subject;
        assembly.IsNew.Should().BeFalse();

        var type = assembly.Namespaces.Should().ContainSingle().Subject.Types.Should().ContainSingle(t => t.FullyQualifiedName == typeFqn).Subject;
        var existingMember = type.Members.Should().ContainSingle(m => m.FullyQualifiedName == memberFqn).Subject;
        var newMember = type.Members.Should().ContainSingle(m => m.FullyQualifiedName!.EndsWith("NewWork()")).Subject;

        existingMember.IsNew.Should().BeFalse();
        existingMember.Metrics[MetricIdentifier.RoslynMaintainabilityIndex].Value.Should().Be(80);
        existingMember.Metrics[MetricIdentifier.RoslynMaintainabilityIndex].Delta.Should().Be(5);
        existingMember.Metrics[MetricIdentifier.RoslynMaintainabilityIndex].Status.Should().Be(ThresholdStatus.Success);
        existingMember.Metrics[MetricIdentifier.AltCoverSequenceCoverage].Value.Should().Be(95);
        existingMember.Metrics[MetricIdentifier.SarifCaRuleViolations].Value.Should().Be(1);

        newMember.IsNew.Should().BeTrue();
        newMember.Metrics[MetricIdentifier.RoslynMaintainabilityIndex].Value.Should().Be(55);
        newMember.Metrics[MetricIdentifier.RoslynMaintainabilityIndex].Delta.Should().BeNull();
        newMember.Metrics[MetricIdentifier.RoslynMaintainabilityIndex].Status.Should().Be(ThresholdStatus.Warning);
    }

    private static MetricValue Metric(decimal value, string unit)
        => new()
        {
            Value = value,
            Unit = unit,
            Status = ThresholdStatus.NotApplicable
        };

    private static MetricsReport CreateBaselineReport(string assemblyName, string namespaceFqn, string typeFqn, string memberFqn, decimal maintainability)
    {
        var member = new MemberMetricsNode
        {
            Name = "DoWork()",
            FullyQualifiedName = memberFqn,
            Metrics = new Dictionary<MetricIdentifier, MetricValue>
            {
                [MetricIdentifier.RoslynMaintainabilityIndex] = new MetricValue
                {
                    Value = maintainability,
                    Unit = "score",
                    Status = ThresholdStatus.NotApplicable
                }
            }
        };

        var type = new TypeMetricsNode
        {
            Name = "SampleType",
            FullyQualifiedName = typeFqn,
            Metrics = new Dictionary<MetricIdentifier, MetricValue>(),
            Members = new List<MemberMetricsNode> { member }
        };

        var @namespace = new NamespaceMetricsNode
        {
            Name = namespaceFqn,
            FullyQualifiedName = namespaceFqn,
            Metrics = new Dictionary<MetricIdentifier, MetricValue>(),
            Types = new List<TypeMetricsNode> { type }
        };

        var assembly = new AssemblyMetricsNode
        {
            Name = assemblyName,
            FullyQualifiedName = assemblyName,
            Metrics = new Dictionary<MetricIdentifier, MetricValue>(),
            Namespaces = new List<NamespaceMetricsNode> { @namespace }
        };

        var solution = new SolutionMetricsNode
        {
            Name = "SampleSolution",
            FullyQualifiedName = "SampleSolution",
            Metrics = new Dictionary<MetricIdentifier, MetricValue>(),
            Assemblies = new List<AssemblyMetricsNode> { assembly }
        };

        return new MetricsReport
        {
            Metadata = new ReportMetadata(),
            Solution = solution
        };
    }
}

