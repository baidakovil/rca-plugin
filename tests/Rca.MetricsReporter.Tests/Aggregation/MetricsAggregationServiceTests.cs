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
        // Use normalized FQN format (with ...) to match what the normalization produces
        const string memberFqn = "Sample.Namespace.SampleType.DoWork(...)";
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
                new(CodeElementKind.Member, "NewWork", $"{typeFqn}.NewWork(...)")
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
        var newMember = type.Members.Should().ContainSingle(m => m.FullyQualifiedName!.EndsWith("NewWork(...)")).Subject;

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

    [Test]
    public void BuildReport_ExcludesConstructorMethods_FromAltCover()
    {
        // Arrange
        const string assemblyName = "Sample.Assembly";
        const string typeFqn = "Sample.Namespace.SampleType";
        const string constructorFqn = "Sample.Namespace.SampleType..ctor(...)";
        const string staticConstructorFqn = "Sample.Namespace.SampleType..cctor(...)";
        const string normalMethodFqn = "Sample.Namespace.SampleType.DoWork(...)";

        var altCoverDocument = new ParsedMetricsDocument
        {
            Elements = new List<ParsedCodeElement>
            {
                new(CodeElementKind.Assembly, assemblyName, assemblyName),
                new(CodeElementKind.Type, "Sample.Namespace.SampleType", typeFqn)
                {
                    ParentFullyQualifiedName = assemblyName
                },
                new(CodeElementKind.Member, ".ctor", constructorFqn)
                {
                    ParentFullyQualifiedName = typeFqn,
                    Metrics = new Dictionary<MetricIdentifier, MetricValue>
                    {
                        [MetricIdentifier.AltCoverSequenceCoverage] = Metric(100, "percent")
                    }
                },
                new(CodeElementKind.Member, ".cctor", staticConstructorFqn)
                {
                    ParentFullyQualifiedName = typeFqn,
                    Metrics = new Dictionary<MetricIdentifier, MetricValue>
                    {
                        [MetricIdentifier.AltCoverSequenceCoverage] = Metric(100, "percent")
                    }
                },
                new(CodeElementKind.Member, "DoWork", normalMethodFqn)
                {
                    ParentFullyQualifiedName = typeFqn,
                    Metrics = new Dictionary<MetricIdentifier, MetricValue>
                    {
                        [MetricIdentifier.AltCoverSequenceCoverage] = Metric(95, "percent")
                    }
                }
            }
        };

        var input = new MetricsAggregationInput
        {
            SolutionName = "SampleSolution",
            AltCoverDocuments = new List<ParsedMetricsDocument> { altCoverDocument },
            RoslynDocuments = new List<ParsedMetricsDocument>(),
            SarifDocuments = new List<ParsedMetricsDocument>(),
            Baseline = null,
            Thresholds = thresholds,
            Paths = new ReportPaths()
        };

        // Act
        var report = service.BuildReport(input);

        // Assert
        var assembly = report.Solution.Assemblies.Should().ContainSingle(a => a.Name == assemblyName).Subject;
        var type = assembly.Namespaces.Should().ContainSingle().Subject.Types.Should().ContainSingle(t => t.FullyQualifiedName == typeFqn).Subject;
        
        // Constructors should be excluded
        type.Members.Should().NotContain(m => m.FullyQualifiedName == constructorFqn);
        type.Members.Should().NotContain(m => m.FullyQualifiedName == staticConstructorFqn);
        
        // Normal method should be included
        type.Members.Should().ContainSingle(m => m.FullyQualifiedName == normalMethodFqn);
    }

    [Test]
    public void BuildReport_ExcludesConstructorMethods_FromRoslyn()
    {
        // Arrange
        const string assemblyName = "Sample.Assembly";
        const string namespaceFqn = "Sample.Namespace";
        const string typeFqn = "Sample.Namespace.SampleType";
        // Roslyn format: constructor name matches type name
        const string constructorFqn = "Sample.Namespace.SampleType.SampleType(...)";
        const string normalMethodFqn = "Sample.Namespace.SampleType.DoWork(...)";

        var roslynDocument = new ParsedMetricsDocument
        {
            Elements = new List<ParsedCodeElement>
            {
                new(CodeElementKind.Assembly, assemblyName, assemblyName),
                new(CodeElementKind.Namespace, namespaceFqn, namespaceFqn)
                {
                    ParentFullyQualifiedName = assemblyName
                },
                new(CodeElementKind.Type, "SampleType", typeFqn)
                {
                    ParentFullyQualifiedName = namespaceFqn
                },
                new(CodeElementKind.Member, "SampleType", constructorFqn)
                {
                    ParentFullyQualifiedName = typeFqn,
                    Metrics = new Dictionary<MetricIdentifier, MetricValue>
                    {
                        [MetricIdentifier.RoslynMaintainabilityIndex] = Metric(80, "score")
                    }
                },
                new(CodeElementKind.Member, "DoWork", normalMethodFqn)
                {
                    ParentFullyQualifiedName = typeFqn,
                    Metrics = new Dictionary<MetricIdentifier, MetricValue>
                    {
                        [MetricIdentifier.RoslynMaintainabilityIndex] = Metric(80, "score")
                    }
                }
            }
        };

        var input = new MetricsAggregationInput
        {
            SolutionName = "SampleSolution",
            AltCoverDocuments = new List<ParsedMetricsDocument>(),
            RoslynDocuments = new List<ParsedMetricsDocument> { roslynDocument },
            SarifDocuments = new List<ParsedMetricsDocument>(),
            Baseline = null,
            Thresholds = thresholds,
            Paths = new ReportPaths()
        };

        // Act
        var report = service.BuildReport(input);

        // Assert
        var assembly = report.Solution.Assemblies.Should().ContainSingle(a => a.Name == assemblyName).Subject;
        var type = assembly.Namespaces.Should().ContainSingle().Subject.Types.Should().ContainSingle(t => t.FullyQualifiedName == typeFqn).Subject;
        
        // Constructor should be excluded
        type.Members.Should().NotContain(m => m.FullyQualifiedName == constructorFqn);
        
        // Normal method should be included
        type.Members.Should().ContainSingle(m => m.FullyQualifiedName == normalMethodFqn);
    }

    [Test]
    public void BuildReport_ExcludesCompilerGeneratedMethods()
    {
        // Arrange
        const string assemblyName = "Sample.Assembly";
        const string namespaceFqn = "Sample.Namespace";
        const string typeFqn = "Sample.Namespace.SampleType";
        const string moveNextFqn = "Sample.Namespace.SampleType.MoveNext(...)";
        const string setStateMachineFqn = "Sample.Namespace.SampleType.SetStateMachine(...)";
        const string moveNextAsyncFqn = "Sample.Namespace.SampleType.MoveNextAsync(...)";
        const string disposeAsyncFqn = "Sample.Namespace.SampleType.DisposeAsync(...)";
        const string normalMethodFqn = "Sample.Namespace.SampleType.DoWork(...)";

        var roslynDocument = new ParsedMetricsDocument
        {
            Elements = new List<ParsedCodeElement>
            {
                new(CodeElementKind.Assembly, assemblyName, assemblyName),
                new(CodeElementKind.Namespace, namespaceFqn, namespaceFqn)
                {
                    ParentFullyQualifiedName = assemblyName
                },
                new(CodeElementKind.Type, "SampleType", typeFqn)
                {
                    ParentFullyQualifiedName = namespaceFqn
                },
                new(CodeElementKind.Member, "MoveNext", moveNextFqn)
                {
                    ParentFullyQualifiedName = typeFqn,
                    Metrics = new Dictionary<MetricIdentifier, MetricValue>
                    {
                        [MetricIdentifier.RoslynMaintainabilityIndex] = Metric(80, "score")
                    }
                },
                new(CodeElementKind.Member, "SetStateMachine", setStateMachineFqn)
                {
                    ParentFullyQualifiedName = typeFqn,
                    Metrics = new Dictionary<MetricIdentifier, MetricValue>
                    {
                        [MetricIdentifier.RoslynMaintainabilityIndex] = Metric(80, "score")
                    }
                },
                new(CodeElementKind.Member, "MoveNextAsync", moveNextAsyncFqn)
                {
                    ParentFullyQualifiedName = typeFqn,
                    Metrics = new Dictionary<MetricIdentifier, MetricValue>
                    {
                        [MetricIdentifier.RoslynMaintainabilityIndex] = Metric(80, "score")
                    }
                },
                new(CodeElementKind.Member, "DisposeAsync", disposeAsyncFqn)
                {
                    ParentFullyQualifiedName = typeFqn,
                    Metrics = new Dictionary<MetricIdentifier, MetricValue>
                    {
                        [MetricIdentifier.RoslynMaintainabilityIndex] = Metric(80, "score")
                    }
                },
                new(CodeElementKind.Member, "DoWork", normalMethodFqn)
                {
                    ParentFullyQualifiedName = typeFqn,
                    Metrics = new Dictionary<MetricIdentifier, MetricValue>
                    {
                        [MetricIdentifier.RoslynMaintainabilityIndex] = Metric(80, "score")
                    }
                }
            }
        };

        var input = new MetricsAggregationInput
        {
            SolutionName = "SampleSolution",
            AltCoverDocuments = new List<ParsedMetricsDocument>(),
            RoslynDocuments = new List<ParsedMetricsDocument> { roslynDocument },
            SarifDocuments = new List<ParsedMetricsDocument>(),
            Baseline = null,
            Thresholds = thresholds,
            Paths = new ReportPaths()
        };

        // Act
        var report = service.BuildReport(input);

        // Assert
        var assembly = report.Solution.Assemblies.Should().ContainSingle(a => a.Name == assemblyName).Subject;
        var type = assembly.Namespaces.Should().ContainSingle().Subject.Types.Should().ContainSingle(t => t.FullyQualifiedName == typeFqn).Subject;
        
        // Compiler-generated methods should be excluded
        type.Members.Should().NotContain(m => m.FullyQualifiedName == moveNextFqn);
        type.Members.Should().NotContain(m => m.FullyQualifiedName == setStateMachineFqn);
        type.Members.Should().NotContain(m => m.FullyQualifiedName == moveNextAsyncFqn);
        type.Members.Should().NotContain(m => m.FullyQualifiedName == disposeAsyncFqn);
        
        // Normal method should be included
        type.Members.Should().ContainSingle(m => m.FullyQualifiedName == normalMethodFqn);
    }

    [Test]
    public void BuildReport_ExcludedMethods_NotInJsonOutput()
    {
        // Arrange
        const string assemblyName = "Sample.Assembly";
        const string typeFqn = "Sample.Namespace.SampleType";
        const string constructorFqn = "Sample.Namespace.SampleType..ctor(...)";
        const string moveNextFqn = "Sample.Namespace.SampleType.MoveNext(...)";
        const string normalMethodFqn = "Sample.Namespace.SampleType.DoWork(...)";

        var roslynDocument = new ParsedMetricsDocument
        {
            Elements = new List<ParsedCodeElement>
            {
                new(CodeElementKind.Assembly, assemblyName, assemblyName),
                new(CodeElementKind.Namespace, "Sample.Namespace", "Sample.Namespace")
                {
                    ParentFullyQualifiedName = assemblyName
                },
                new(CodeElementKind.Type, "SampleType", typeFqn)
                {
                    ParentFullyQualifiedName = "Sample.Namespace"
                },
                new(CodeElementKind.Member, ".ctor", constructorFqn)
                {
                    ParentFullyQualifiedName = typeFqn,
                    Metrics = new Dictionary<MetricIdentifier, MetricValue>
                    {
                        [MetricIdentifier.RoslynMaintainabilityIndex] = Metric(80, "score")
                    }
                },
                new(CodeElementKind.Member, "MoveNext", moveNextFqn)
                {
                    ParentFullyQualifiedName = typeFqn,
                    Metrics = new Dictionary<MetricIdentifier, MetricValue>
                    {
                        [MetricIdentifier.RoslynMaintainabilityIndex] = Metric(80, "score")
                    }
                },
                new(CodeElementKind.Member, "DoWork", normalMethodFqn)
                {
                    ParentFullyQualifiedName = typeFqn,
                    Metrics = new Dictionary<MetricIdentifier, MetricValue>
                    {
                        [MetricIdentifier.RoslynMaintainabilityIndex] = Metric(80, "score")
                    }
                }
            }
        };

        var input = new MetricsAggregationInput
        {
            SolutionName = "SampleSolution",
            AltCoverDocuments = new List<ParsedMetricsDocument>(),
            RoslynDocuments = new List<ParsedMetricsDocument> { roslynDocument },
            SarifDocuments = new List<ParsedMetricsDocument>(),
            Baseline = null,
            Thresholds = thresholds,
            Paths = new ReportPaths()
        };

        // Act
        var report = service.BuildReport(input);

        // Assert - Verify that excluded methods are not in the report structure
        // (which means they won't be in JSON either)
        var assembly = report.Solution.Assemblies.Should().ContainSingle(a => a.Name == assemblyName).Subject;
        var type = assembly.Namespaces.Should().ContainSingle().Subject.Types.Should().ContainSingle(t => t.FullyQualifiedName == typeFqn).Subject;
        
        // Excluded methods should not be in members list
        type.Members.Should().NotContain(m => m.FullyQualifiedName == constructorFqn);
        type.Members.Should().NotContain(m => m.FullyQualifiedName == moveNextFqn);
        
        // Normal method should be included
        type.Members.Should().ContainSingle(m => m.FullyQualifiedName == normalMethodFqn);
        
        // Verify that only the normal method is present
        type.Members.Should().HaveCount(1);
        type.Members[0].FullyQualifiedName.Should().Be(normalMethodFqn);
    }

    [Test]
    public void BuildReport_ExcludedAssemblies_AreNotAddedToSolution()
    {
        // Arrange
        const string includedAssembly = "Rca.Network";
        const string excludedNamespace = "Rca.UI.Tests";
        const string excludedTypeFqn = "Rca.UI.Tests.RcaDockablePanelViewModelTests";

        var assemblyFilter = AssemblyFilter.FromString("Tests");
        var serviceWithFilter = new MetricsAggregationService(new MemberFilter(), assemblyFilter);

        var roslynDocument = new ParsedMetricsDocument
        {
            SolutionName = "SampleSolution",
            Elements = new List<ParsedCodeElement>
            {
                new(CodeElementKind.Assembly, includedAssembly, includedAssembly),
                new(CodeElementKind.Namespace, excludedNamespace, excludedNamespace),
                new(CodeElementKind.Type, "RcaDockablePanelViewModelTests", excludedTypeFqn)
                {
                    ParentFullyQualifiedName = excludedNamespace
                }
            }
        };

        var input = new MetricsAggregationInput
        {
            SolutionName = "SampleSolution",
            AltCoverDocuments = new List<ParsedMetricsDocument>(),
            RoslynDocuments = new List<ParsedMetricsDocument> { roslynDocument },
            SarifDocuments = new List<ParsedMetricsDocument>(),
            Baseline = null,
            Thresholds = thresholds,
            Paths = new ReportPaths()
        };

        // Act
        var report = serviceWithFilter.BuildReport(input);

        // Assert
        var assembly = report.Solution.Assemblies.Should().ContainSingle(a => a.Name == includedAssembly).Subject;
        assembly.Namespaces.Should().BeEmpty("namespaces from excluded assemblies must be removed");
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
            Name = "DoWork",
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

