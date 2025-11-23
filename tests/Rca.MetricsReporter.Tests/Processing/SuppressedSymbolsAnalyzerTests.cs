namespace Rca.MetricsReporter.Tests.Processing;

using System;
using System.IO;
using System.Threading;
using FluentAssertions;
using NUnit.Framework;
using Rca.Tools.MetricsReporter.Processing;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Integration-style tests for <see cref="SuppressedSymbolsAnalyzer"/> that verify
/// discovery of <c>SuppressMessage</c> attributes in real C# source files.
/// </summary>
[TestFixture]
[Category("Unit")]
public sealed class SuppressedSymbolsAnalyzerTests
{
  private string _rootDirectory = null!;

  [SetUp]
  public void SetUp()
  {
    _rootDirectory = Path.Combine(Path.GetTempPath(), "RcaMetricsReporter_Suppressed_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(_rootDirectory);
  }

  [TearDown]
  public void TearDown()
  {
    try
    {
      if (Directory.Exists(_rootDirectory))
      {
        Directory.Delete(_rootDirectory, recursive: true);
      }
    }
    catch
    {
      // Best effort cleanup; tests must not fail on IO issues during teardown.
    }
  }

  [Test]
  public void Analyze_ClassLevelSuppression_IsDiscovered()
  {
    // Arrange
    var srcDir = Path.Combine(_rootDirectory, "src", "Sample.Assembly");
    Directory.CreateDirectory(srcDir);

    var code = """
      using System.Diagnostics.CodeAnalysis;

      namespace Sample.Namespace;

      [SuppressMessage(
          "Microsoft.Maintainability",
          "CA1506:Avoid excessive class coupling",
          Justification = "Test justification.")]
      public class SampleType
      {
      }
      """;

    var filePath = Path.Combine(srcDir, "SampleType.cs");
    File.WriteAllText(filePath, code);

    var cancellationToken = CancellationToken.None;

    // Act
    var sourceCodeFolders = new[] { "src" };
    var report = SuppressedSymbolsAnalyzer.Analyze(_rootDirectory, sourceCodeFolders, excludedAssemblyNames: null, cancellationToken);

    // Assert
    report.SuppressedSymbols.Should().NotBeEmpty("class-level SuppressMessage should be discovered");
    var entry = report.SuppressedSymbols[0];
    entry.RuleId.Should().Be("CA1506");
    entry.Metric.Should().Be("RoslynClassCoupling");
    entry.FullyQualifiedName.Should().Be("Sample.Namespace.SampleType");
    entry.Justification.Should().Be("Test justification.");
  }

  [Test]
  public void Analyze_RealSolutionRoot_FindsPipeTestExecutionTransportSuppression()
  {
    // Arrange
    var testDirectory = TestContext.CurrentContext.TestDirectory;
    // Go up from tests/Rca.MetricsReporter.Tests/bin/Debug/net8.0-windows to solution root.
    var solutionRoot = Path.GetFullPath(Path.Combine(
      testDirectory,
      "..", "..", "..", "..", ".."));

    var excludedAssemblyNames = "Tests,Contracts,MetricsReporter";
    var sourceCodeFolders = new[] { "src", "src/Tools", "tests" };

    // Act
    var report = SuppressedSymbolsAnalyzer.Analyze(solutionRoot, sourceCodeFolders, excludedAssemblyNames, CancellationToken.None);

    // Assert
    report.SuppressedSymbols.Should().NotBeEmpty("real SuppressMessage usages (e.g. in PipeTestExecutionTransport) should be discovered");
    report.SuppressedSymbols.Should().Contain(
      s =>
        s.RuleId == "CA1506" &&
        s.Metric == nameof(MetricIdentifier.RoslynClassCoupling) &&
        s.FullyQualifiedName == "Rca.TestAdapter.PipeTestExecutionTransport.Execute(...)",
      "CA1506 suppression on PipeTestExecutionTransport.Execute should be mapped to RoslynClassCoupling at member level");
  }
}


