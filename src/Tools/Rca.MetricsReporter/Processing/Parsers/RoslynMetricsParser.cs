namespace Rca.Tools.MetricsReporter.Processing.Parsers;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Парсер отчётов Microsoft.CodeAnalysis.Metrics.
/// </summary>
public sealed class RoslynMetricsParser : IMetricsSourceParser
{
    private static readonly XNamespace XmlNamespace = XNamespace.None;

    private static readonly IReadOnlyDictionary<string, MetricIdentifier> MetricMap =
        new Dictionary<string, MetricIdentifier>(StringComparer.OrdinalIgnoreCase)
        {
            ["MaintainabilityIndex"] = MetricIdentifier.RoslynMaintainabilityIndex,
            ["CyclomaticComplexity"] = MetricIdentifier.RoslynCyclomaticComplexity,
            ["ClassCoupling"] = MetricIdentifier.RoslynClassCoupling,
            ["DepthOfInheritance"] = MetricIdentifier.RoslynDepthOfInheritance,
            ["SourceLines"] = MetricIdentifier.RoslynSourceLines,
            ["ExecutableLines"] = MetricIdentifier.RoslynExecutableLines
        };

    /// <inheritdoc />
    public async Task<ParsedMetricsDocument> ParseAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);

        await using var stream = File.OpenRead(path);
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken).ConfigureAwait(false);

        var targets = document.Element(XmlNamespace + "CodeMetricsReport")
                              ?.Element(XmlNamespace + "Targets")
                              ?.Elements(XmlNamespace + "Target")
                              ?? Enumerable.Empty<XElement>();

        var elements = new List<ParsedCodeElement>();
        var solutionName = string.Empty;

        foreach (var target in targets)
        {
            solutionName = target.Attribute("Name")?.Value ?? solutionName;

            var assemblyElement = target.Element(XmlNamespace + "Assembly");
            if (assemblyElement is null)
            {
                continue;
            }

            var assemblyName = assemblyElement.Attribute("Name")?.Value ?? "<unknown-assembly>";
            var assemblyFqn = ExtractAssemblyShortName(assemblyName);
            var assemblyNode = new ParsedCodeElement(CodeElementKind.Assembly, assemblyFqn, assemblyFqn)
            {
                Metrics = ExtractMetrics(assemblyElement.Element(XmlNamespace + "Metrics")),
                Source = null,
                ParentFullyQualifiedName = null
            };
            elements.Add(assemblyNode);

            foreach (var namespaceElement in assemblyElement.Element(XmlNamespace + "Namespaces")?.Elements(XmlNamespace + "Namespace")
                     ?? Enumerable.Empty<XElement>())
            {
                ProcessNamespace(elements, namespaceElement, assemblyNode);
            }
        }

        return new ParsedMetricsDocument
        {
            SolutionName = solutionName,
            Elements = elements
        };
    }

    private static void ProcessNamespace(ICollection<ParsedCodeElement> collector, XElement namespaceElement, ParsedCodeElement assemblyNode)
    {
        var namespaceName = namespaceElement.Attribute("Name")?.Value ?? "<global>";
        var namespaceNode = new ParsedCodeElement(CodeElementKind.Namespace, namespaceName, namespaceName)
        {
            ParentFullyQualifiedName = assemblyNode.FullyQualifiedName,
            Metrics = ExtractMetrics(namespaceElement.Element(XmlNamespace + "Metrics"))
        };
        collector.Add(namespaceNode);

        foreach (var typeElement in namespaceElement.Element(XmlNamespace + "Types")?.Elements() ?? Enumerable.Empty<XElement>())
        {
            ProcessType(collector, typeElement, namespaceNode, namespaceName);
        }
    }

    private static void ProcessType(ICollection<ParsedCodeElement> collector, XElement typeElement, ParsedCodeElement namespaceNode, string namespaceName)
    {
        var typeName = typeElement.Attribute("Name")?.Value ?? "<unknown-type>";
        var typeFqn = string.IsNullOrWhiteSpace(namespaceName) || namespaceName == "<global>"
            ? typeName
            : $"{namespaceName}.{typeName}";

        var source = CreateSourceLocation(typeElement.Attribute("File")?.Value, typeElement.Attribute("Line")?.Value);
        var typeNode = new ParsedCodeElement(CodeElementKind.Type, typeName, typeFqn)
        {
            ParentFullyQualifiedName = namespaceNode.FullyQualifiedName,
            Metrics = ExtractMetrics(typeElement.Element(XmlNamespace + "Metrics")),
            Source = source
        };

        collector.Add(typeNode);

        var members = typeElement.Element(XmlNamespace + "Members");
        if (members is not null)
        {
            foreach (var member in members.Elements())
            {
                ProcessMember(collector, member, typeNode);
            }
        }
    }

    private static void ProcessMember(ICollection<ParsedCodeElement> collector, XElement memberElement, ParsedCodeElement typeNode)
    {
        var memberName = memberElement.Attribute("Name")?.Value ?? "<unknown-member>";
        var memberDisplayName = ExtractMemberDisplayName(memberName);
        var memberFqn = BuildMemberFqn(typeNode.FullyQualifiedName, memberDisplayName, typeNode.Name);
        var source = CreateSourceLocation(memberElement.Attribute("File")?.Value, memberElement.Attribute("Line")?.Value);

        var memberNode = new ParsedCodeElement(CodeElementKind.Member, memberDisplayName, memberFqn)
        {
            ParentFullyQualifiedName = typeNode.FullyQualifiedName,
            Metrics = ExtractMetrics(memberElement.Element(XmlNamespace + "Metrics")),
            Source = source
        };

        collector.Add(memberNode);
    }

    private static IDictionary<MetricIdentifier, MetricValue> ExtractMetrics(XElement? metricsElement)
    {
        var result = new Dictionary<MetricIdentifier, MetricValue>();
        if (metricsElement is null)
        {
            return result;
        }

        foreach (var metricElement in metricsElement.Elements(XmlNamespace + "Metric"))
        {
            var name = metricElement.Attribute("Name")?.Value;
            if (name is null || !MetricMap.TryGetValue(name, out var identifier))
            {
                continue;
            }

            var valueAttribute = metricElement.Attribute("Value")?.Value;
            if (valueAttribute is null)
            {
                continue;
            }

            if (!decimal.TryParse(valueAttribute, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            var unit = identifier is MetricIdentifier.RoslynSourceLines or MetricIdentifier.RoslynExecutableLines
                ? "count"
                : "score";

            result[identifier] = new MetricValue
            {
                Value = value,
                Unit = unit,
                Status = ThresholdStatus.NotApplicable
            };
        }

        return result;
    }

    private static SourceLocation? CreateSourceLocation(string? file, string? line)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return null;
        }

        int? lineNumber = null;
        if (int.TryParse(line, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLine))
        {
            lineNumber = parsedLine;
        }

        return new SourceLocation
        {
            Path = file,
            StartLine = lineNumber,
            EndLine = lineNumber
        };
    }

    private static string ExtractAssemblyShortName(string assemblyDisplayName)
    {
        var commaIndex = assemblyDisplayName.IndexOf(',', StringComparison.Ordinal);
        return commaIndex >= 0 ? assemblyDisplayName[..commaIndex] : assemblyDisplayName;
    }

    private static string ExtractMemberDisplayName(string rawName)
    {
        var spaceIndex = rawName.IndexOf(' ');
        return spaceIndex >= 0 ? rawName[(spaceIndex + 1)..] : rawName;
    }

    private static string? BuildMemberFqn(string? typeFqn, string memberDisplayName, string typeName)
    {
        if (typeFqn is null)
        {
            return memberDisplayName;
        }

        var suffix = memberDisplayName.StartsWith(typeName + ".", StringComparison.Ordinal)
            ? memberDisplayName[(typeName.Length + 1)..]
            : memberDisplayName;

        return $"{typeFqn}.{suffix}";
    }
}

