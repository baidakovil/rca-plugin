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
/// Parses Microsoft.CodeAnalysis.Metrics XML reports.
/// </summary>
public sealed class RoslynMetricsParser : IMetricsSourceParser
{
  private static readonly XNamespace XmlNamespace = XNamespace.None;

  private static readonly Dictionary<string, MetricIdentifier> MetricMap =
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

    var targets = document
        .Element(XmlNamespace + "CodeMetricsReport")
        ?.Element(XmlNamespace + "Targets")
        ?.Elements(XmlNamespace + "Target")
        ?? Enumerable.Empty<XElement>();

    var elements = new List<ParsedCodeElement>();
    var solutionName = string.Empty;

    foreach (var target in targets)
    {
      solutionName = target.Attribute("Name")?.Value ?? solutionName;

      if (target.Element(XmlNamespace + "Assembly") is { } assemblyElement)
      {
        elements.AddRange(ParseAssembly(assemblyElement));
      }
    }

    return new ParsedMetricsDocument
    {
      SolutionName = solutionName,
      Elements = elements
    };
  }

  private static IEnumerable<ParsedCodeElement> ParseAssembly(XElement assemblyElement)
  {
    var assemblyName = assemblyElement.Attribute("Name")?.Value ?? "<unknown-assembly>";
    var assemblyFqn = ExtractAssemblyShortName(assemblyName);

    var assemblyNode = new ParsedCodeElement(CodeElementKind.Assembly, assemblyFqn, assemblyFqn)
    {
      Metrics = ExtractMetrics(assemblyElement.Element(XmlNamespace + "Metrics"))
    };

    yield return assemblyNode;

    var namespaces = assemblyElement.Element(XmlNamespace + "Namespaces")?.Elements(XmlNamespace + "Namespace")
                     ?? Enumerable.Empty<XElement>();

    foreach (var ns in namespaces.SelectMany(namespaceElement => ParseNamespace(namespaceElement, assemblyNode)))
    {
      yield return ns;
    }
  }

  private static IEnumerable<ParsedCodeElement> ParseNamespace(XElement namespaceElement, ParsedCodeElement assemblyNode)
  {
    var namespaceName = namespaceElement.Attribute("Name")?.Value ?? "<global>";
    var namespaceNode = new ParsedCodeElement(CodeElementKind.Namespace, namespaceName, namespaceName)
    {
      ParentFullyQualifiedName = assemblyNode.FullyQualifiedName,
      ContainingAssemblyName = assemblyNode.FullyQualifiedName ?? assemblyNode.Name,
      Metrics = ExtractMetrics(namespaceElement.Element(XmlNamespace + "Metrics"))
    };

    yield return namespaceNode;

    var types = namespaceElement.Element(XmlNamespace + "Types")?.Elements() ?? Enumerable.Empty<XElement>();

    var assemblyName = assemblyNode.FullyQualifiedName ?? assemblyNode.Name;
    foreach (var typeNode in types.SelectMany(type => ParseType(type, namespaceNode, namespaceName, assemblyName)))
    {
      yield return typeNode;
    }
  }

  private static IEnumerable<ParsedCodeElement> ParseType(
      XElement typeElement,
      ParsedCodeElement namespaceNode,
      string namespaceName,
      string? assemblyName)
  {
    var typeName = typeElement.Attribute("Name")?.Value ?? "<unknown-type>";
    var typeFqn = string.IsNullOrWhiteSpace(namespaceName) || namespaceName == "<global>"
        ? typeName
        : $"{namespaceName}.{typeName}";

    var source = CreateSourceLocation(typeElement.Attribute("File")?.Value, typeElement.Attribute("Line")?.Value);
    var typeNode = new ParsedCodeElement(CodeElementKind.Type, typeName, typeFqn)
    {
      ParentFullyQualifiedName = namespaceNode.FullyQualifiedName,
      ContainingAssemblyName = assemblyName,
      Metrics = ExtractMetrics(typeElement.Element(XmlNamespace + "Metrics")),
      Source = source
    };

    yield return typeNode;

    var members = typeElement.Element(XmlNamespace + "Members")?.Elements() ?? Enumerable.Empty<XElement>();
    foreach (var memberNode in members.Select(member => ParseMember(member, typeNode)))
    {
      yield return memberNode;
    }
  }

  private static ParsedCodeElement ParseMember(XElement memberElement, ParsedCodeElement typeNode)
  {
    var memberName = memberElement.Attribute("Name")?.Value ?? "<unknown-member>";
    var memberDisplayName = ExtractMemberDisplayName(memberName);

    var memberFqn = BuildMemberFqn(typeNode.FullyQualifiedName, memberDisplayName, typeNode.Name);
    var normalizedMemberFqn = SymbolNormalizer.NormalizeFullyQualifiedMethodName(memberFqn);

    var methodNameOnly = ExtractDisplayMethodName(memberName, memberDisplayName, typeNode.Name);
    var source = CreateSourceLocation(memberElement.Attribute("File")?.Value, memberElement.Attribute("Line")?.Value);

    return new ParsedCodeElement(CodeElementKind.Member, methodNameOnly, normalizedMemberFqn)
    {
      ParentFullyQualifiedName = typeNode.FullyQualifiedName,
      ContainingAssemblyName = typeNode.ContainingAssemblyName,
      Metrics = ExtractMetrics(memberElement.Element(XmlNamespace + "Metrics")),
      Source = source
    };
  }

  private static string ExtractDisplayMethodName(string rawMemberName, string memberDisplayName, string typeName)
  {
    var typeNameDot = typeName + ".";
    if (memberDisplayName.StartsWith(typeNameDot, StringComparison.Ordinal))
    {
      var afterTypeNameDot = memberDisplayName[typeNameDot.Length..];
      if (afterTypeNameDot.StartsWith(typeName + "(", StringComparison.Ordinal))
      {
        return typeName;
      }
    }

    return SymbolNormalizer.ExtractMethodName(rawMemberName)
        ?? SymbolNormalizer.ExtractMethodName(memberDisplayName)
        ?? memberDisplayName;
  }

  private static Dictionary<MetricIdentifier, MetricValue> ExtractMetrics(XElement? metricsElement)
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
    // Remove return type prefix (format: "ReturnType Method(...)")
    // This needs to handle complex return types like:
    // - "Task<string> Method(...)"
    // - "Task<(bool, string?)> Method(...)"
    // - "void Method(...)"

    // Find the first space that's not inside angle brackets (for generic types)
    // or parentheses (for tuple types)
    var depth = 0;
    var angleDepth = 0;
    var spaceIndex = -1;

    for (var i = 0; i < rawName.Length; i++)
    {
      var ch = rawName[i];

      if (ch == '<')
      {
        angleDepth++;
      }
      else if (ch == '>')
      {
        angleDepth--;
      }
      else if (ch == '(' && angleDepth == 0)
      {
        depth++;
      }
      else if (ch == ')' && angleDepth == 0)
      {
        depth--;
      }
      else if (ch == ' ' && depth == 0 && angleDepth == 0)
      {
        // Found a space outside of brackets and parentheses - this is the separator
        spaceIndex = i;
        break;
      }
    }

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

