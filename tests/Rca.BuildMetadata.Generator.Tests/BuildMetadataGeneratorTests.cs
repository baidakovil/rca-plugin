using System.Diagnostics.CodeAnalysis;
using System.Text;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

namespace Rca.BuildMetadata.Generator.Tests;

/// <summary>
/// Integration-style tests that exercise <see cref="BuildMetadataGenerator"/> end-to-end via <see cref="CSharpGeneratorDriver"/>.
/// Verifies that diagnostics and generated sources react to various MSBuild configuration combinations.
/// </summary>
[TestFixture]
public sealed class BuildMetadataGeneratorTests
{
  private Dictionary<string, string?> _baseProperties = null!;

  private static readonly IReadOnlyList<MetadataReference> DefaultReferences = ReferenceFactory.Create();

  [SetUp]
  public void SetUp()
  {
    _baseProperties = TestPropertyBag.Create();
  }

  [Test]
  [Category("Unit")]
  public void Execute_InvalidSourceHashLength_ReportsRca002()
  {
    // Arrange
    var overrides = new Dictionary<string, string?>
    {
      ["build_property.RcaSourceHashLength"] = "0"
    };

    // Act
    var result = RunGenerator(overrides);

    // Assert
    result.Diagnostics.Should().ContainSingle(d => d.Id == "RCA002");
    result.Results.Single().GeneratedSources.Should().BeEmpty("generator should exit early when validation fails");
  }

  [Test]
  [Category("Integration")]
  public void Execute_LoaderProjectMissingHash_ReportsWarnings()
  {
    // Arrange
    var overrides = new Dictionary<string, string?>
    {
      ["build_property.IsLoaderGroupProject"] = "true",
      ["build_property.MSBuildProjectName"] = "Rca.Loader"
    };

    // Act
    var result = RunGenerator(overrides);

    // Assert
    result.Diagnostics.Should().Contain(d => d.Id == "RCA022" && d.Severity == DiagnosticSeverity.Warning);
    result.Diagnostics.Should().Contain(d => d.Id == "RCA018" && d.Severity == DiagnosticSeverity.Warning);
  }

  [Test]
  [Category("Integration")]
  public void Execute_LoaderProjectWithHash_GeneratesAssemblyMetadata()
  {
    // Arrange
    var overrides = new Dictionary<string, string?>
    {
      ["build_property.IsLoaderGroupProject"] = "true",
      ["build_property.MSBuildProjectName"] = "Rca.Loader"
    };

    var additionalTexts = new AdditionalText[]
    {
      new InMemoryAdditionalText("SourceHash-Loader-abc123.txt", "abc123"),
      new InMemoryAdditionalText("unrelated.txt", null)
    };

    // Act
    var result = RunGenerator(overrides, additionalTexts);

    // Assert
    result.Diagnostics.Should().BeEmpty("valid inputs should not raise warnings");

    var generated = result.Results.Single().GeneratedSources;
    generated.Should().Contain(gs => gs.HintName == "Rca.AssemblyMetadata.g.cs");

    var metadataSource = generated.Single(gs => gs.HintName == "Rca.AssemblyMetadata.g.cs").SourceText.ToString();
    metadataSource.Should().Contain("abc123");
    metadataSource.Should().Contain(TestPropertyBag.DefaultTimestamp);
  }

  [Test]
  [Category("Integration")]
  public void Execute_RcaContractsProject_GeneratesBuildMetadataClass()
  {
    // Arrange
    var overrides = new Dictionary<string, string?>
    {
      ["build_property.MSBuildProjectName"] = "Rca.Contracts",
      ["build_property.RcaLoaderProjectsList"] = "Rca.Loader;Rca.Loader.Ui;;",
      ["build_property.RcaRuntimeProjectsList"] = "Rca.Runtime",
      ["build_property.RcaCommandPipeName"] = "LoaderPipe",
      ["build_property.RcaLogPipeName"] = "LogPipe",
      ["build_property.RcaForceNewStamp"] = "1"
    };

    // Act
    var result = RunGenerator(overrides);

    // Assert
    var generated = result.Results.Single().GeneratedSources;
    generated.Should().Contain(gs => gs.HintName == "RcaBuildMetadata.g.cs");

    var metadataSource = generated.Single(gs => gs.HintName == "RcaBuildMetadata.g.cs").SourceText.ToString();
    metadataSource.Should().Contain("public static class RcaBuildMetadata");
    metadataSource.Should().Contain("LoaderProjects => new[] { \"Rca.Loader\", \"Rca.Loader.Ui\" }");
    metadataSource.Should().Contain("RuntimeProjects => new[] { \"Rca.Runtime\" }");
    metadataSource.Should().Contain("ForceNewStamp => true");
    metadataSource.Should().Contain("CommandPipeName => @\"LoaderPipe\"");
    metadataSource.Should().Contain("LogPipeName => \"LogPipe\"");
  }

  [Test]
  [Category("Integration")]
  public void Execute_RuntimeProjectWithoutTimestamp_ReportsRca019()
  {
    // Arrange
    var overrides = new Dictionary<string, string?>
    {
      ["build_property.IsRuntimeGroupProject"] = "true",
      ["build_property.MSBuildProjectName"] = "Rca.Runtime",
      ["build_property.RcaHotReloadTimestamp"] = ""
    };

    var additionalTexts = new AdditionalText[]
    {
      new InMemoryAdditionalText("SourceHash-Runtime-def456.txt", "def456")
    };

    // Act
    var result = RunGenerator(overrides, additionalTexts);

    // Assert
    result.Diagnostics.Should().ContainSingle(d => d.Id == "RCA019");
    result.Results.Single().GeneratedSources.Should().BeEmpty("missing timestamp prevents metadata emission");
  }

  [Test]
  [Category("Integration")]
  public void Execute_LoaderMarkerReadFailure_ReportsRca020()
  {
    // Arrange
    var overrides = new Dictionary<string, string?>
    {
      ["build_property.IsLoaderGroupProject"] = "true"
    };

    var additionalTexts = new AdditionalText[]
    {
      new InMemoryAdditionalText("SourceHash-Loader-error.txt", "ignored", throwOnRead: true)
    };

    // Act
    var result = RunGenerator(overrides, additionalTexts);

    // Assert
    result.Diagnostics.Should().Contain(d => d.Id == "RCA020");
    result.Diagnostics.Should().Contain(d => d.Id == "RCA022");
  }

  private GeneratorDriverRunResult RunGenerator(
      IDictionary<string, string?>? overrides = null,
      IReadOnlyCollection<AdditionalText>? additionalTexts = null)
  {
    var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp12);
    var syntaxTree = CSharpSyntaxTree.ParseText("public class Dummy {}", parseOptions);

    var compilation = CSharpCompilation.Create(
        assemblyName: "GeneratorTestAssembly",
        syntaxTrees: new[] { syntaxTree },
        references: DefaultReferences,
        options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    var mergedOptions = TestPropertyBag.Merge(_baseProperties, overrides);
    var provider = new TestAnalyzerConfigOptionsProvider(mergedOptions);
    var driver = CSharpGeneratorDriver.Create(
        generators: new ISourceGenerator[] { new BuildMetadataGenerator() },
        additionalTexts: additionalTexts?.ToArray() ?? Array.Empty<AdditionalText>(),
        parseOptions: parseOptions,
        optionsProvider: provider);

    return driver.RunGenerators(compilation).GetRunResult();
  }
}

internal static class TestPropertyBag
{
  public const string DefaultTimestamp = "20241201_120000";

  public static Dictionary<string, string?> Create()
  {
    return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
    {
      ["build_property.RcaSourceHashLength"] = "8",
      ["build_property.RcaTimestampPattern"] = "yyyyMMdd_HHmmss",
      ["build_property.RcaRevitAddinsDir"] = @"C:\Revit\Addins",
      ["build_property.RcaTestDeployRoot"] = @"C:\Deploy",
      ["build_property.RcaLogRoot"] = @"C:\Logs",
      ["build_property.RcaRevitVersion"] = "2026",
      ["build_property.RcaRevitLibsPath"] = @"C:\Revit\Libs",
      ["build_property.RcaTimestampFile"] = @"C:\Deploy\timestamp.txt",
      ["build_property.RcaStickyStampSeconds"] = "30",
      ["build_property.RcaForceNewStamp"] = "false",
      ["build_property.RcaLoaderProjectsList"] = string.Empty,
      ["build_property.RcaRuntimeProjectsList"] = string.Empty,
      ["build_property.MSBuildProjectName"] = "Rca.Loader",
      ["build_property.RcaHotReloadTimestamp"] = DefaultTimestamp,
      ["build_property.RcaCommandPipeName"] = string.Empty,
      ["build_property.RcaLogPipeName"] = string.Empty,
      ["build_property.IsLoaderGroupProject"] = "false",
      ["build_property.IsRuntimeGroupProject"] = "false"
    };
  }

  public static Dictionary<string, string?> Merge(
      IReadOnlyDictionary<string, string?> baseline,
      IDictionary<string, string?>? overrides)
  {
    var merged = new Dictionary<string, string?>(baseline, StringComparer.OrdinalIgnoreCase);

    if (overrides == null)
      return merged;

    foreach (var pair in overrides)
    {
      merged[pair.Key] = pair.Value;
    }

    return merged;
  }
}

internal sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
  private static readonly AnalyzerConfigOptions EmptyOptions = new DictionaryAnalyzerConfigOptions(new Dictionary<string, string?>());

  private readonly AnalyzerConfigOptions _globalOptions;

  public TestAnalyzerConfigOptionsProvider(IDictionary<string, string?> globalOptions)
  {
    _globalOptions = new DictionaryAnalyzerConfigOptions(globalOptions);
  }

  public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

  public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => EmptyOptions;

  public override AnalyzerConfigOptions GetOptions(AdditionalText text) => EmptyOptions;
}

internal sealed class DictionaryAnalyzerConfigOptions : AnalyzerConfigOptions
{
  private readonly IReadOnlyDictionary<string, string?> _values;

  public DictionaryAnalyzerConfigOptions(IDictionary<string, string?> values)
  {
    _values = new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase);
  }

  public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
  {
    return _values.TryGetValue(key, out value);
  }
}

internal sealed class InMemoryAdditionalText : AdditionalText
{
  private readonly string? _content;
  private readonly bool _throwOnRead;

  public InMemoryAdditionalText(string path, string? content, bool throwOnRead = false)
  {
    Path = path ?? throw new ArgumentNullException(nameof(path));
    _content = content;
    _throwOnRead = throwOnRead;
  }

  public override string Path { get; }

  public override SourceText? GetText(CancellationToken cancellationToken = default)
  {
    if (_throwOnRead)
      throw new InvalidOperationException("Simulated marker file failure");

    return _content is null ? null : SourceText.From(_content, Encoding.UTF8);
  }
}

internal static class ReferenceFactory
{
  public static IReadOnlyList<MetadataReference> Create()
  {
    var assemblies = new[]
    {
      typeof(object).Assembly,
      typeof(Enumerable).Assembly,
      typeof(Attribute).Assembly,
      typeof(Uri).Assembly,
      typeof(Console).Assembly,
      typeof(StringBuilder).Assembly,
      typeof(NUnit.Framework.Assert).Assembly
    };

    var references = new List<MetadataReference>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var assembly in assemblies)
    {
      if (seen.Add(assembly.Location))
      {
        references.Add(MetadataReference.CreateFromFile(assembly.Location));
      }
    }

    return references;
  }
}

