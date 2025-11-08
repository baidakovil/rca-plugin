namespace Rca.MetricsReporter.Tests.Processing.Parsers;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using NUnit.Framework;
using Rca.Tools.MetricsReporter.Model;
using Rca.Tools.MetricsReporter.Processing;
using Rca.Tools.MetricsReporter.Processing.Parsers;

/// <summary>
/// Unit tests for <see cref="AltCoverMetricsParser"/> class.
/// </summary>
/// <remarks>
/// These tests verify that AltCoverMetricsParser correctly normalizes method signatures
/// from AltCover XML format to unified format, ensuring symbols can be properly merged
/// with symbols from other sources (e.g., Roslyn).
/// </remarks>
[TestFixture]
[Category("Unit")]
public sealed class AltCoverMetricsParserTests
{
    private AltCoverMetricsParser parser = null!;

    [SetUp]
    public void SetUp()
    {
        parser = new AltCoverMetricsParser();
    }

    [Test]
    public async Task ParseAsync_MethodWithFullQualifiedTypes_NormalizesParameters()
    {
        // Arrange
        var xml = CreateAltCoverXml(
            assemblyName: "Rca.Loader",
            className: "Rca.Loader.LoaderApp",
            methodName: "void Rca.Loader.LoaderApp.OnApplicationIdling(System.Object, Autodesk.Revit.UI.Events.IdlingEventArgs)",
            methodCoverage: 50.0m);

        var tempFile = CreateTempFile(xml);

        try
        {
            // Act
            var result = await parser.ParseAsync(tempFile, CancellationToken.None);

            // Assert
            result.Elements.Should().NotBeEmpty();
            var member = result.Elements.FirstOrDefault(e => e.Kind == CodeElementKind.Member);
            member.Should().NotBeNull();
            member!.FullyQualifiedName.Should().Be("Rca.Loader.LoaderApp.OnApplicationIdling(...)");
            member.Name.Should().Be("OnApplicationIdling");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ParseAsync_MethodWithoutFullTypePath_AppendsTypePath()
    {
        // Arrange
        var xml = CreateAltCoverXml(
            assemblyName: "Rca.Loader",
            className: "Rca.Loader.LoaderApp",
            methodName: "void OnApplicationIdling(System.Object, Autodesk.Revit.UI.Events.IdlingEventArgs)",
            methodCoverage: 50.0m);

        var tempFile = CreateTempFile(xml);

        try
        {
            // Act
            var result = await parser.ParseAsync(tempFile, CancellationToken.None);

            // Assert
            result.Elements.Should().NotBeEmpty();
            var member = result.Elements.FirstOrDefault(e => e.Kind == CodeElementKind.Member);
            member.Should().NotBeNull();
            member!.FullyQualifiedName.Should().Be("Rca.Loader.LoaderApp.OnApplicationIdling(...)");
            member.Name.Should().Be("OnApplicationIdling");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ParseAsync_MethodWithCppStyleOperators_NormalizesCorrectly()
    {
        // Arrange - AltCover sometimes uses :: for namespace separator
        var xml = CreateAltCoverXml(
            assemblyName: "Rca.Loader",
            className: "Rca.Loader.LoaderApp",
            methodName: "void Rca::Loader::LoaderApp::OnApplicationIdling(System.Object)",
            methodCoverage: 50.0m);

        var tempFile = CreateTempFile(xml);

        try
        {
            // Act
            var result = await parser.ParseAsync(tempFile, CancellationToken.None);

            // Assert
            result.Elements.Should().NotBeEmpty();
            var member = result.Elements.FirstOrDefault(e => e.Kind == CodeElementKind.Member);
            member.Should().NotBeNull();
            member!.FullyQualifiedName.Should().Contain("OnApplicationIdling(...)");
            member.FullyQualifiedName.Should().NotContain("::");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ParseAsync_NestedType_NormalizesCorrectly()
    {
        // Arrange - nested types use / separator in AltCover
        var xml = CreateAltCoverXml(
            assemblyName: "Rca.Loader",
            className: "Rca.Loader.LoaderApp/NestedClass",
            methodName: "void Method(System.String)",
            methodCoverage: 50.0m);

        var tempFile = CreateTempFile(xml);

        try
        {
            // Act
            var result = await parser.ParseAsync(tempFile, CancellationToken.None);

            // Assert
            result.Elements.Should().NotBeEmpty();
            var type = result.Elements.FirstOrDefault(e => e.Kind == CodeElementKind.Type);
            type.Should().NotBeNull();
            type!.FullyQualifiedName.Should().Contain("+"); // Nested types should use + separator
            type.FullyQualifiedName.Should().NotContain("/");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ParseAsync_MethodWithoutParameters_NormalizesCorrectly()
    {
        // Arrange
        var xml = CreateAltCoverXml(
            assemblyName: "Rca.Loader",
            className: "Rca.Loader.LoaderApp",
            methodName: "void Method()",
            methodCoverage: 50.0m);

        var tempFile = CreateTempFile(xml);

        try
        {
            // Act
            var result = await parser.ParseAsync(tempFile, CancellationToken.None);

            // Assert
            result.Elements.Should().NotBeEmpty();
            var member = result.Elements.FirstOrDefault(e => e.Kind == CodeElementKind.Member);
            member.Should().NotBeNull();
            member!.FullyQualifiedName.Should().Be("Rca.Loader.LoaderApp.Method(...)");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ParseAsync_PropertyGetter_NormalizesCorrectly()
    {
        // Arrange - property getter from real data
        var xml = CreateAltCoverXml(
            assemblyName: "Rca.Network",
            className: "Rca.Network.NetworkPlaceholder",
            methodName: "System.Boolean get_IsReady()",
            methodCoverage: 100.0m);

        var tempFile = CreateTempFile(xml);

        try
        {
            // Act
            var result = await parser.ParseAsync(tempFile, CancellationToken.None);

            // Assert
            result.Elements.Should().NotBeEmpty();
            var member = result.Elements.FirstOrDefault(e => e.Kind == CodeElementKind.Member);
            member.Should().NotBeNull();
            member!.FullyQualifiedName.Should().Be("Rca.Network.NetworkPlaceholder.get_IsReady(...)");
            member.Name.Should().Be("get_IsReady");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ParseAsync_MultipleMethods_SameType_NormalizesAll()
    {
        // Arrange
        var xml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<CoverageSession>
  <Modules>
    <Module>
      <ModuleName>Rca.Loader</ModuleName>
      <Classes>
        <Class>
          <FullName>Rca.Loader.LoaderApp</FullName>
          <Methods>
            <Method sequenceCoverage=""50"">
              <Name>void OnApplicationIdling(System.Object, Autodesk.Revit.UI.Events.IdlingEventArgs)</Name>
            </Method>
            <Method sequenceCoverage=""100"">
              <Name>void OnShutdown(Autodesk.Revit.UI.UIControlledApplication)</Name>
            </Method>
          </Methods>
        </Class>
      </Classes>
    </Module>
  </Modules>
</CoverageSession>";

        var tempFile = CreateTempFile(xml);

        try
        {
            // Act
            var result = await parser.ParseAsync(tempFile, CancellationToken.None);

            // Assert
            var members = result.Elements.Where(e => e.Kind == CodeElementKind.Member).ToList();
            members.Should().HaveCount(2);
            members[0].FullyQualifiedName.Should().Be("Rca.Loader.LoaderApp.OnApplicationIdling(...)");
            members[1].FullyQualifiedName.Should().Be("Rca.Loader.LoaderApp.OnShutdown(...)");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ParseAsync_ComplexGenericParameters_NormalizesCorrectly()
    {
        // Arrange
        var xml = CreateAltCoverXml(
            assemblyName: "Rca.Loader",
            className: "Rca.Loader.LoaderApp",
            methodName: "void ProcessData(System.Collections.Generic.List<System.String>, System.Collections.Generic.Dictionary<System.String, System.Int32>)",
            methodCoverage: 50.0m);

        var tempFile = CreateTempFile(xml);

        try
        {
            // Act
            var result = await parser.ParseAsync(tempFile, CancellationToken.None);

            // Assert
            result.Elements.Should().NotBeEmpty();
            var member = result.Elements.FirstOrDefault(e => e.Kind == CodeElementKind.Member);
            member.Should().NotBeNull();
            member!.FullyQualifiedName.Should().Be("Rca.Loader.LoaderApp.ProcessData(...)");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ParseAsync_EmptyModules_ReturnsEmptyDocument()
    {
        // Arrange
        var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<CoverageSession>
  <Modules />
</CoverageSession>";

        var tempFile = CreateTempFile(xml);

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
    public async Task ParseAsync_RealWorld_ToStringMethod_NormalizesCorrectly()
    {
        // Arrange - from real AltCover data
        var xml = CreateAltCoverXml(
            assemblyName: "Rca.Logging.Contracts",
            className: "Rca.Logging.Contracts.LogEntryDto",
            methodName: "System.String Rca.Logging.Contracts.LogEntryDto::ToString()",
            methodCoverage: 0.0m);

        var tempFile = CreateTempFile(xml);

        try
        {
            // Act
            var result = await parser.ParseAsync(tempFile, CancellationToken.None);

            // Assert
            var member = result.Elements.FirstOrDefault(e => e.Kind == CodeElementKind.Member);
            member.Should().NotBeNull();
            member!.FullyQualifiedName.Should().Be("Rca.Logging.Contracts.LogEntryDto.ToString(...)");
            member.Name.Should().Be("ToString");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ParseAsync_RealWorld_OperatorEquality_NormalizesCorrectly()
    {
        // Arrange - from real AltCover data
        var xml = CreateAltCoverXml(
            assemblyName: "Rca.Logging.Contracts",
            className: "Rca.Logging.Contracts.LogEntryDto",
            methodName: "System.Boolean Rca.Logging.Contracts.LogEntryDto::op_Equality(Rca.Logging.Contracts.LogEntryDto,Rca.Logging.Contracts.LogEntryDto)",
            methodCoverage: 100.0m);

        var tempFile = CreateTempFile(xml);

        try
        {
            // Act
            var result = await parser.ParseAsync(tempFile, CancellationToken.None);

            // Assert
            var member = result.Elements.FirstOrDefault(e => e.Kind == CodeElementKind.Member);
            member.Should().NotBeNull();
            member!.FullyQualifiedName.Should().Be("Rca.Logging.Contracts.LogEntryDto.op_Equality(...)");
            member.Name.Should().Be("op_Equality");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ParseAsync_RealWorld_EqualsMethod_NormalizesCorrectly()
    {
        // Arrange - from real AltCover data
        var xml = CreateAltCoverXml(
            assemblyName: "Rca.Logging.Contracts",
            className: "Rca.Logging.Contracts.LogEntryDto",
            methodName: "System.Boolean Rca.Logging.Contracts.LogEntryDto::Equals(Rca.Logging.Contracts.LogEntryDto)",
            methodCoverage: 100.0m);

        var tempFile = CreateTempFile(xml);

        try
        {
            // Act
            var result = await parser.ParseAsync(tempFile, CancellationToken.None);

            // Assert
            var member = result.Elements.FirstOrDefault(e => e.Kind == CodeElementKind.Member);
            member.Should().NotBeNull();
            member!.FullyQualifiedName.Should().Be("Rca.Logging.Contracts.LogEntryDto.Equals(...)");
            member.Name.Should().Be("Equals");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ParseAsync_RealWorld_CloneMethod_NormalizesCorrectly()
    {
        // Arrange - from real AltCover data
        var xml = CreateAltCoverXml(
            assemblyName: "Rca.Logging.Contracts",
            className: "Rca.Logging.Contracts.LogEntryDto",
            methodName: "Rca.Logging.Contracts.LogEntryDto Rca.Logging.Contracts.LogEntryDto::<Clone>$()",
            methodCoverage: 100.0m);

        var tempFile = CreateTempFile(xml);

        try
        {
            // Act
            var result = await parser.ParseAsync(tempFile, CancellationToken.None);

            // Assert
            var member = result.Elements.FirstOrDefault(e => e.Kind == CodeElementKind.Member);
            member.Should().NotBeNull();
            member!.FullyQualifiedName.Should().Be("Rca.Logging.Contracts.LogEntryDto.<Clone>$(...)");
            member.Name.Should().Be("<Clone>$");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ParseAsync_RealWorld_Constructor_NormalizesCorrectly()
    {
        // Arrange - from real AltCover data
        var xml = CreateAltCoverXml(
            assemblyName: "Rca.UI",
            className: "Rca.UI.Services.ServiceResolver",
            methodName: "System.Void Rca.UI.Services.ServiceResolver::.ctor(Rca.Contracts.Infrastructure.ServiceContainer)",
            methodCoverage: 100.0m);

        var tempFile = CreateTempFile(xml);

        try
        {
            // Act
            var result = await parser.ParseAsync(tempFile, CancellationToken.None);

            // Assert
            var member = result.Elements.FirstOrDefault(e => e.Kind == CodeElementKind.Member);
            member.Should().NotBeNull();
            member!.FullyQualifiedName.Should().Be("Rca.UI.Services.ServiceResolver..ctor(...)");
            member.Name.Should().Be(".ctor");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ParseAsync_RealWorld_GenericMethodRegister_NormalizesCorrectly()
    {
        // Arrange - from real AltCover data
        var xml = CreateAltCoverXml(
            assemblyName: "Rca.Loader.Contracts",
            className: "Rca.Loader.Contracts.SharedServiceRegistry",
            methodName: "System.Void Rca.Loader.Contracts.SharedServiceRegistry::Register(TInterface)",
            methodCoverage: 87.5m);

        var tempFile = CreateTempFile(xml);

        try
        {
            // Act
            var result = await parser.ParseAsync(tempFile, CancellationToken.None);

            // Assert
            var member = result.Elements.FirstOrDefault(e => e.Kind == CodeElementKind.Member);
            member.Should().NotBeNull();
            member!.FullyQualifiedName.Should().Be("Rca.Loader.Contracts.SharedServiceRegistry.Register(...)");
            member.Name.Should().Be("Register");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ParseAsync_RealWorld_GenericMethodResolve_NormalizesCorrectly()
    {
        // Arrange - from real AltCover data
        var xml = CreateAltCoverXml(
            assemblyName: "Rca.Loader.Contracts",
            className: "Rca.Loader.Contracts.SharedServiceRegistry",
            methodName: "TInterface Rca.Loader.Contracts.SharedServiceRegistry::Resolve()",
            methodCoverage: 87.5m);

        var tempFile = CreateTempFile(xml);

        try
        {
            // Act
            var result = await parser.ParseAsync(tempFile, CancellationToken.None);

            // Assert
            var member = result.Elements.FirstOrDefault(e => e.Kind == CodeElementKind.Member);
            member.Should().NotBeNull();
            member!.FullyQualifiedName.Should().Be("Rca.Loader.Contracts.SharedServiceRegistry.Resolve(...)");
            member.Name.Should().Be("Resolve");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static string CreateAltCoverXml(
        string assemblyName,
        string className,
        string methodName,
        decimal methodCoverage)
    {
        // Escape XML special characters in methodName (especially < and > for generic types)
        var escapedMethodName = methodName
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
        
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<CoverageSession>
  <Modules>
    <Module>
      <ModuleName>{assemblyName}</ModuleName>
      <Summary sequenceCoverage=""{methodCoverage}"" branchCoverage=""{methodCoverage}"" />
      <Classes>
        <Class>
          <FullName>{className}</FullName>
          <Summary sequenceCoverage=""{methodCoverage}"" branchCoverage=""{methodCoverage}"" />
            <Methods>
            <Method sequenceCoverage=""{methodCoverage}"" branchCoverage=""{methodCoverage}"" cyclomaticComplexity=""1"" nPathComplexity=""1"">
              <Name>{escapedMethodName}</Name>
            </Method>
          </Methods>
        </Class>
      </Classes>
    </Module>
  </Modules>
</CoverageSession>";
    }

    private static string CreateTempFile(string content)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".xml");
        File.WriteAllText(tempFile, content);
        return tempFile;
    }
}

