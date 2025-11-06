namespace Rca.Tools.MetricsReporter.Processing.Parsers;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Парсер отчётов AltCover/OpenCover.
/// </summary>
public sealed class AltCoverMetricsParser : IMetricsSourceParser
{
    private static readonly XNamespace XmlNamespace = XNamespace.None;

    /// <inheritdoc />
    public async Task<ParsedMetricsDocument> ParseAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);

        await using var stream = System.IO.File.OpenRead(path);
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken).ConfigureAwait(false);

        var coverageSession = document.Element(XmlNamespace + "CoverageSession")
                             ?? throw new InvalidOperationException("CoverageSession root element not found.");

        var modulesElement = coverageSession.Element(XmlNamespace + "Modules");
        if (modulesElement is null)
        {
            return new ParsedMetricsDocument
            {
                SolutionName = string.Empty,
                Elements = Array.Empty<ParsedCodeElement>()
            };
        }

        var elements = new List<ParsedCodeElement>();
        foreach (var module in modulesElement.Elements(XmlNamespace + "Module"))
        {
            var assemblyName = module.Element(XmlNamespace + "ModuleName")?.Value ?? "<unknown-assembly>";
            var assemblyNode = CreateNode(CodeElementKind.Assembly, assemblyName, assemblyName, null, null);
            PopulateMetrics(assemblyNode.Metrics, module.Element(XmlNamespace + "Summary"));
            elements.Add(assemblyNode);

            var files = module.Element(XmlNamespace + "Files")?
                .Elements(XmlNamespace + "File")
                .Select(file => new
                {
                    Id = file.Attribute("uid")?.Value,
                    Path = file.Attribute("fullPath")?.Value
                })
                .Where(file => file.Id is not null && file.Path is not null)
                .ToDictionary(file => file.Id!, file => file.Path!, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var @class in module.Element(XmlNamespace + "Classes")?.Elements(XmlNamespace + "Class") ?? Enumerable.Empty<XElement>())
            {
                var className = @class.Element(XmlNamespace + "FullName")?.Value ?? "<unknown-class>";
                var classNode = CreateNode(CodeElementKind.Type, className, NormalizeTypeName(className), assemblyName, null);
                PopulateMetrics(classNode.Metrics, @class.Element(XmlNamespace + "Summary"));
                elements.Add(classNode);

                foreach (var method in @class.Element(XmlNamespace + "Methods")?.Elements(XmlNamespace + "Method") ?? Enumerable.Empty<XElement>())
                {
                    var methodName = method.Element(XmlNamespace + "Name")?.Value ?? "<unknown-method>";
                    var methodFqn = NormalizeMethodName(methodName);
                    var sourceLocation = ResolveSourceLocation(method, files);
                    var memberNode = CreateNode(CodeElementKind.Member, methodName, methodFqn, classNode.FullyQualifiedName, sourceLocation);
                    PopulateMethodMetrics(memberNode.Metrics, method);

                    elements.Add(memberNode);
                }
            }
        }

        return new ParsedMetricsDocument
        {
            SolutionName = string.Empty,
            Elements = elements
        };
    }

    private static ParsedCodeElement CreateNode(CodeElementKind kind, string name, string? fqn, string? parentFqn, SourceLocation? source)
        => new(kind, name, fqn)
        {
            ParentFullyQualifiedName = parentFqn,
            Source = source
        };

    private static SourceLocation? ResolveSourceLocation(XElement methodElement, IReadOnlyDictionary<string, string> files)
    {
        var fileRef = methodElement.Element(XmlNamespace + "FileRef");
        var fileId = fileRef?.Attribute("uid")?.Value;
        if (fileId is null || !files.TryGetValue(fileId, out var path))
        {
            return null;
        }

        var sequencePoints = methodElement.Element(XmlNamespace + "SequencePoints")?.Elements(XmlNamespace + "SequencePoint");
        if (sequencePoints is null || !sequencePoints.Any())
        {
            return new SourceLocation { Path = path };
        }

        var minLine = sequencePoints.Min(SeqStartLine);
        var maxLine = sequencePoints.Max(SeqEndLine);

        return new SourceLocation
        {
            Path = path,
            StartLine = minLine,
            EndLine = maxLine
        };
    }

    private static int SeqStartLine(XElement point) => (int)(point.Attribute("sl")?.GetDecimalValue() ?? 0m);
    private static int SeqEndLine(XElement point) => (int)(point.Attribute("el")?.GetDecimalValue() ?? 0m);

    private static void PopulateMetrics(IDictionary<MetricIdentifier, MetricValue> target, XElement? summary)
    {
        if (summary is null)
        {
            return;
        }

        AddMetric(target, MetricIdentifier.AltCoverSequenceCoverage, summary.Attribute("sequenceCoverage"));
        AddMetric(target, MetricIdentifier.AltCoverBranchCoverage, summary.Attribute("branchCoverage"));

        AddMetric(target, MetricIdentifier.AltCoverCyclomaticComplexity, summary.Attribute("maxCyclomaticComplexity"), unit: "count");
        AddMetric(target, MetricIdentifier.AltCoverNPathComplexity, summary.Attribute("maxNPathComplexity"), unit: "count");
    }

    private static void PopulateMethodMetrics(IDictionary<MetricIdentifier, MetricValue> target, XElement method)
    {
        AddMetric(target, MetricIdentifier.AltCoverSequenceCoverage, method.Attribute("sequenceCoverage"));
        AddMetric(target, MetricIdentifier.AltCoverBranchCoverage, method.Attribute("branchCoverage"));
        AddMetric(target, MetricIdentifier.AltCoverCyclomaticComplexity, method.Attribute("cyclomaticComplexity"), unit: "count");
        AddMetric(target, MetricIdentifier.AltCoverNPathComplexity, method.Attribute("nPathComplexity"), unit: "count");
    }

    private static void AddMetric(IDictionary<MetricIdentifier, MetricValue> target, MetricIdentifier identifier, XAttribute? attribute, string unit = "percent")
    {
        if (attribute is null)
        {
            return;
        }

        var value = attribute.GetDecimalValue();
        if (value is null)
        {
            return;
        }

        target[identifier] = new MetricValue
        {
            Value = value,
            Unit = unit,
            Status = ThresholdStatus.NotApplicable
        };
    }

    private static string? NormalizeTypeName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        // AltCover форматирует nested types как Namespace.Type/Nested
        return fullName.Replace('/', '+');
    }

    private static string? NormalizeMethodName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        var spaceIndex = fullName.IndexOf(' ');
        var signature = spaceIndex >= 0 ? fullName[(spaceIndex + 1)..] : fullName;
        signature = signature.Replace("::", ".", StringComparison.Ordinal);
        signature = signature.Replace('/', '+');
        return signature;
    }
}

file static class XmlExtensions
{
    public static decimal? GetDecimalValue(this XAttribute? attribute)
    {
        if (attribute?.Value is null)
        {
            return null;
        }

        return decimal.TryParse(attribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}

