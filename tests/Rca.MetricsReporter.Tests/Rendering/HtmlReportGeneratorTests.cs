namespace Rca.MetricsReporter.Tests.Rendering;

using System;
using System.Collections.Generic;
using System.IO;
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

    [Test]
    public void Generate_WithCoverageHtmlDir_GeneratesLinksForTypes()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var type = new TypeMetricsNode
            {
                Name = "SampleType",
                FullyQualifiedName = "Sample.Namespace.SampleType",
                Metrics = new Dictionary<MetricIdentifier, MetricValue>(),
                Members = new List<MemberMetricsNode>()
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

            // Create coverage HTML file
            var htmlFileName = "Sample.Assembly_SampleType.html";
            var htmlFilePath = Path.Combine(tempDir, htmlFileName);
            File.WriteAllText(htmlFilePath, "<html></html>");

            var generator = new HtmlReportGenerator();

            // Act
            var html = generator.Generate(report, tempDir);

            // Assert
            html.Should().Contain("SampleType");
            html.Should().Contain("coverage-link-type");
            html.Should().Contain(htmlFileName);
            html.Should().Contain("file://");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Test]
    public void Generate_WithCoverageHtmlDirButMissingFile_DoesNotGenerateLinks()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var type = new TypeMetricsNode
            {
                Name = "SampleType",
                FullyQualifiedName = "Sample.Namespace.SampleType",
                Metrics = new Dictionary<MetricIdentifier, MetricValue>(),
                Members = new List<MemberMetricsNode>()
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

            // Do NOT create coverage HTML file
            var generator = new HtmlReportGenerator();

            // Act
            var html = generator.Generate(report, tempDir);

            // Assert
            html.Should().Contain("SampleType");
            // Verify that no link with coverage-link-type class is generated
            html.Should().NotMatchRegex(@"<a[^>]*coverage-link-type[^>]*>");
            html.Should().NotContain("file://");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Test]
    public void Generate_WithoutCoverageHtmlDir_DoesNotGenerateLinks()
    {
        // Arrange
        var type = new TypeMetricsNode
        {
            Name = "SampleType",
            FullyQualifiedName = "Sample.Namespace.SampleType",
            Metrics = new Dictionary<MetricIdentifier, MetricValue>(),
            Members = new List<MemberMetricsNode>()
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

        // Act
        var html = generator.Generate(report, null);

        // Assert
        html.Should().Contain("SampleType");
        // Verify that no link with coverage-link-type class is generated
        html.Should().NotMatchRegex(@"<a[^>]*coverage-link-type[^>]*>");
    }
}

