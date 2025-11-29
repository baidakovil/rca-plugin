namespace Rca.MetricsReporter.Tests.Rendering;

using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using Rca.Tools.MetricsReporter.Model;
using Rca.Tools.MetricsReporter.Rendering;

/// <summary>
/// Unit tests for <see cref="TableRendererInitializer"/> class.
/// </summary>
[TestFixture]
[Category("Unit")]
public sealed class TableRendererInitializerTests
{
  [Test]
  public void InitializeAndAssign_WithValidInputs_InitializesAllComponents()
  {
    // Arrange
    var metricOrder = new[] { MetricIdentifier.RoslynClassCoupling };
    var metricUnits = new Dictionary<MetricIdentifier, string?> { [MetricIdentifier.RoslynClassCoupling] = null };
    var report = new MetricsReport
    {
      Metadata = new ReportMetadata
      {
        SuppressedSymbols = new List<SuppressedSymbolInfo>()
      },
      Solution = new SolutionMetricsNode
      {
        Name = "SampleSolution",
        FullyQualifiedName = "SampleSolution",
        Metrics = new Dictionary<MetricIdentifier, MetricValue>(),
        Assemblies = new List<AssemblyMetricsNode>()
      }
    };

    // Act
    TableRendererInitializer.InitializeAndAssign(
      metricOrder,
      metricUnits,
      report,
      null,
      out var coverageLinkBuilder,
      out var suppressedIndex,
      out var stateCalculator,
      out var attributeBuilder,
      out var metricCellRenderer);

    // Assert
    coverageLinkBuilder.Should().BeNull();
    suppressedIndex.Should().NotBeNull();
    stateCalculator.Should().NotBeNull();
    attributeBuilder.Should().NotBeNull();
    metricCellRenderer.Should().NotBeNull();
  }

  [Test]
  public void InitializeAndAssign_WithCoverageHtmlDir_CreatesCoverageLinkBuilder()
  {
    // Arrange
    var metricOrder = new[] { MetricIdentifier.RoslynClassCoupling };
    var metricUnits = new Dictionary<MetricIdentifier, string?> { [MetricIdentifier.RoslynClassCoupling] = null };
    var report = new MetricsReport
    {
      Metadata = new ReportMetadata
      {
        SuppressedSymbols = new List<SuppressedSymbolInfo>()
      },
      Solution = new SolutionMetricsNode
      {
        Name = "SampleSolution",
        FullyQualifiedName = "SampleSolution",
        Metrics = new Dictionary<MetricIdentifier, MetricValue>(),
        Assemblies = new List<AssemblyMetricsNode>()
      }
    };

    // Act
    TableRendererInitializer.InitializeAndAssign(
      metricOrder,
      metricUnits,
      report,
      "C:\\Coverage",
      out var coverageLinkBuilder,
      out var suppressedIndex,
      out var stateCalculator,
      out var attributeBuilder,
      out var metricCellRenderer);

    // Assert
    coverageLinkBuilder.Should().NotBeNull();
  }

  [Test]
  public void InitializeAndAssign_WithEmptyCoverageHtmlDir_DoesNotCreateCoverageLinkBuilder()
  {
    // Arrange
    var metricOrder = new[] { MetricIdentifier.RoslynClassCoupling };
    var metricUnits = new Dictionary<MetricIdentifier, string?> { [MetricIdentifier.RoslynClassCoupling] = null };
    var report = new MetricsReport
    {
      Metadata = new ReportMetadata
      {
        SuppressedSymbols = new List<SuppressedSymbolInfo>()
      },
      Solution = new SolutionMetricsNode
      {
        Name = "SampleSolution",
        FullyQualifiedName = "SampleSolution",
        Metrics = new Dictionary<MetricIdentifier, MetricValue>(),
        Assemblies = new List<AssemblyMetricsNode>()
      }
    };

    // Act
    TableRendererInitializer.InitializeAndAssign(
      metricOrder,
      metricUnits,
      report,
      string.Empty,
      out var coverageLinkBuilder,
      out var suppressedIndex,
      out var stateCalculator,
      out var attributeBuilder,
      out var metricCellRenderer);

    // Assert
    coverageLinkBuilder.Should().BeNull();
  }

  [Test]
  public void InitializeAndAssign_WithWhitespaceCoverageHtmlDir_DoesNotCreateCoverageLinkBuilder()
  {
    // Arrange
    var metricOrder = new[] { MetricIdentifier.RoslynClassCoupling };
    var metricUnits = new Dictionary<MetricIdentifier, string?> { [MetricIdentifier.RoslynClassCoupling] = null };
    var report = new MetricsReport
    {
      Metadata = new ReportMetadata
      {
        SuppressedSymbols = new List<SuppressedSymbolInfo>()
      },
      Solution = new SolutionMetricsNode
      {
        Name = "SampleSolution",
        FullyQualifiedName = "SampleSolution",
        Metrics = new Dictionary<MetricIdentifier, MetricValue>(),
        Assemblies = new List<AssemblyMetricsNode>()
      }
    };

    // Act
    TableRendererInitializer.InitializeAndAssign(
      metricOrder,
      metricUnits,
      report,
      "   ",
      out var coverageLinkBuilder,
      out var suppressedIndex,
      out var stateCalculator,
      out var attributeBuilder,
      out var metricCellRenderer);

    // Assert
    coverageLinkBuilder.Should().BeNull();
  }

  [Test]
  public void InitializeAndAssign_WithSuppressedSymbols_BuildsSuppressedIndex()
  {
    // Arrange
    var metricOrder = new[] { MetricIdentifier.RoslynClassCoupling };
    var metricUnits = new Dictionary<MetricIdentifier, string?> { [MetricIdentifier.RoslynClassCoupling] = null };
    var report = new MetricsReport
    {
      Metadata = new ReportMetadata
      {
        SuppressedSymbols = new List<SuppressedSymbolInfo>
        {
          new()
          {
            FullyQualifiedName = "Sample.Namespace.SampleType",
            Metric = "RoslynClassCoupling",
            RuleId = "CA1506",
            Justification = "Justified"
          }
        }
      },
      Solution = new SolutionMetricsNode
      {
        Name = "SampleSolution",
        FullyQualifiedName = "SampleSolution",
        Metrics = new Dictionary<MetricIdentifier, MetricValue>(),
        Assemblies = new List<AssemblyMetricsNode>()
      }
    };

    // Act
    TableRendererInitializer.InitializeAndAssign(
      metricOrder,
      metricUnits,
      report,
      null,
      out var coverageLinkBuilder,
      out var suppressedIndex,
      out var stateCalculator,
      out var attributeBuilder,
      out var metricCellRenderer);

    // Assert
    suppressedIndex.Should().NotBeNull();
    suppressedIndex!.Should().ContainKey(("Sample.Namespace.SampleType", MetricIdentifier.RoslynClassCoupling));
  }

  [Test]
  public void InitializeAndAssign_WithHierarchy_BuildsDescendantCountIndex()
  {
    // Arrange
    var metricOrder = new[] { MetricIdentifier.RoslynClassCoupling };
    var metricUnits = new Dictionary<MetricIdentifier, string?> { [MetricIdentifier.RoslynClassCoupling] = null };

    var member = new MemberMetricsNode
    {
      Name = "DoWork",
      FullyQualifiedName = "Sample.Namespace.SampleType.DoWork()",
      Metrics = new Dictionary<MetricIdentifier, MetricValue>()
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

    var solution = new SolutionMetricsNode
    {
      Name = "SampleSolution",
      FullyQualifiedName = "SampleSolution",
      Metrics = new Dictionary<MetricIdentifier, MetricValue>(),
      Assemblies = new List<AssemblyMetricsNode> { assembly }
    };

    var report = new MetricsReport
    {
      Metadata = new ReportMetadata
      {
        SuppressedSymbols = new List<SuppressedSymbolInfo>()
      },
      Solution = solution
    };

    // Act
    TableRendererInitializer.InitializeAndAssign(
      metricOrder,
      metricUnits,
      report,
      null,
      out var coverageLinkBuilder,
      out var suppressedIndex,
      out var stateCalculator,
      out var attributeBuilder,
      out var metricCellRenderer);

    // Assert
    stateCalculator.Should().NotBeNull();
    attributeBuilder.Should().NotBeNull();
    metricCellRenderer.Should().NotBeNull();
  }

  [Test]
  public void InitializeAndAssign_WithMultipleMetrics_InitializesCorrectly()
  {
    // Arrange
    var metricOrder = new[]
    {
      MetricIdentifier.RoslynClassCoupling,
      MetricIdentifier.RoslynCyclomaticComplexity,
      MetricIdentifier.RoslynMaintainabilityIndex
    };
    var metricUnits = new Dictionary<MetricIdentifier, string?>
    {
      [MetricIdentifier.RoslynClassCoupling] = null,
      [MetricIdentifier.RoslynCyclomaticComplexity] = null,
      [MetricIdentifier.RoslynMaintainabilityIndex] = "score"
    };
    var report = new MetricsReport
    {
      Metadata = new ReportMetadata
      {
        SuppressedSymbols = new List<SuppressedSymbolInfo>()
      },
      Solution = new SolutionMetricsNode
      {
        Name = "SampleSolution",
        FullyQualifiedName = "SampleSolution",
        Metrics = new Dictionary<MetricIdentifier, MetricValue>(),
        Assemblies = new List<AssemblyMetricsNode>()
      }
    };

    // Act
    TableRendererInitializer.InitializeAndAssign(
      metricOrder,
      metricUnits,
      report,
      null,
      out var coverageLinkBuilder,
      out var suppressedIndex,
      out var stateCalculator,
      out var attributeBuilder,
      out var metricCellRenderer);

    // Assert
    stateCalculator.Should().NotBeNull();
    metricCellRenderer.Should().NotBeNull();
  }
}

