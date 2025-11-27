namespace Rca.Tools.MetricsReporter.Rendering;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Generates the HTML table for the metrics report.
/// Handles recursive rendering of the metrics hierarchy.
/// </summary>
internal sealed class HtmlTableGenerator
{
  private readonly MetricIdentifier[] _metricOrder;
  private readonly IReadOnlyDictionary<MetricIdentifier, string?> _metricUnits;
  private int _idCounter;
  private CoverageLinkBuilder? _coverageLinkBuilder;
  private Dictionary<(string Fqn, MetricIdentifier Metric), SuppressedSymbolInfo>? _suppressedIndex;
  private Dictionary<MetricsNode, int>? _descendantCountIndex;

  /// <summary>
  /// Initializes a new instance of the <see cref="HtmlTableGenerator"/> class.
  /// </summary>
  /// <param name="metricOrder">The order of metrics to display in columns.</param>
  /// <param name="metricUnits">Units associated with each metric.</param>
  public HtmlTableGenerator(MetricIdentifier[] metricOrder, IReadOnlyDictionary<MetricIdentifier, string?> metricUnits)
  {
    _metricOrder = metricOrder ?? throw new ArgumentNullException(nameof(metricOrder));
    _metricUnits = metricUnits ?? throw new ArgumentNullException(nameof(metricUnits));
  }

  /// <summary>
  /// Generates the HTML table markup for the metrics report.
  /// </summary>
  /// <param name="report">The metrics report.</param>
  /// <param name="coverageHtmlDir">Optional path to HTML coverage reports directory for generating hyperlinks.</param>
  /// <returns>HTML markup for the table.</returns>
  public string Generate(MetricsReport report, string? coverageHtmlDir = null)
  {
    ArgumentNullException.ThrowIfNull(report);

    _idCounter = 0;
    _coverageLinkBuilder = string.IsNullOrWhiteSpace(coverageHtmlDir) ? null : new CoverageLinkBuilder(coverageHtmlDir);
    _suppressedIndex = BuildSuppressedIndex(report);
    _descendantCountIndex = BuildDescendantCountIndex(report);
    var builder = new StringBuilder();

    builder.AppendLine("<div class=\"table-container\"> ");
    // Action buttons inside table-container for proper sticky positioning
    builder.AppendLine("<div class=\"table-actions\"> ");
    builder.AppendLine("  <div class=\"status-badges\">");
    builder.AppendLine("    <span class=\"badge status-warning\">Warning</span>");
    builder.AppendLine("    <span class=\"badge status-error\">Error</span>");
    builder.AppendLine("  </div>");
    builder.AppendLine("  <div style=\"flex:1\"></div>");
    builder.AppendLine("  <div class=\"state-filters\" role=\"group\" aria-label=\"Row filters\">");
    builder.AppendLine("    <span class=\"state-filters-label\">Filter to:</span>");
    builder.AppendLine("    <label class=\"state-filter-option\">");
    builder.AppendLine("      <input type=\"checkbox\" id=\"filter-new\" aria-label=\"Show only new rows\" />");
    builder.AppendLine("      <span>new</span>");
    builder.AppendLine("    </label>");
    builder.AppendLine("    <label class=\"state-filter-option\">");
    builder.AppendLine("      <input type=\"checkbox\" id=\"filter-changes\" aria-label=\"Show only rows with metric changes\" />");
    builder.AppendLine("      <span>changes</span>");
    builder.AppendLine("    </label>");
    builder.AppendLine("    <label class=\"state-filter-option\">");
    builder.AppendLine("      <input type=\"checkbox\" id=\"filter-suppressed\" aria-label=\"Show only rows with suppressed metrics\" />");
    builder.AppendLine("      <span>suppressed</span>");
    builder.AppendLine("    </label>");
    builder.AppendLine("  </div>");
    builder.AppendLine("  <div class=\"awareness-control\">");
    builder.AppendLine("    <label for=\"awareness-level\" class=\"awareness-label\">Awareness:</label>");
    builder.AppendLine("    <input type=\"range\" id=\"awareness-level\" min=\"1\" max=\"3\" step=\"1\" value=\"1\" aria-valuemin=\"1\" aria-valuemax=\"3\" aria-valuenow=\"1\" aria-label=\"Awareness level\" />");
    builder.AppendLine("    <span id=\"awareness-label\" class=\"awareness-value\">All</span>");
    builder.AppendLine("  </div>");
    builder.AppendLine("  <div class=\"filter-control\" style=\"margin-right: 50px;\">");
    builder.AppendLine("    <div class=\"filter-input-wrapper\">");
    builder.AppendLine("      <input type=\"text\" id=\"filter-input\" class=\"filter-input\" placeholder=\"Filter:\" aria-label=\"Filter rows by name\" />");
    builder.AppendLine("      <button type=\"button\" id=\"filter-clear\" class=\"filter-clear\" aria-label=\"Clear filter\" style=\"display: none;\">×</button>");
    builder.AppendLine("    </div>");
    builder.AppendLine("  </div>");
  builder.AppendLine("  <div class=\"detail-control\">");
  builder.AppendLine("    <label for=\"detail-level\" class=\"detail-label\">Detailing:</label>");
  builder.AppendLine("    <input type=\"range\" id=\"detail-level\" min=\"1\" max=\"3\" step=\"1\" value=\"2\" aria-valuemin=\"1\" aria-valuemax=\"3\" aria-valuenow=\"2\" aria-label=\"Detail level\" />");
  builder.AppendLine("    <span id=\"detail-label\" class=\"detail-value\">Type</span>");
  builder.AppendLine("  </div>");
    builder.AppendLine("  <button id=\"expand-all\">Expand all</button>");
    builder.AppendLine("  <button id=\"collapse-all\">Collapse all</button>");
    builder.AppendLine("</div>");
    builder.AppendLine("<table id=\"metrics-table\" class=\"metrics stripped\"> ");
    builder.AppendLine("  <thead>");
    // First header row: group labels (AltCover, Roslyn, Sarif)
    builder.AppendLine("    <tr>");
    builder.AppendLine("      <th data-col=\"symbol\" rowspan=\"2\">Symbol</th>");
    builder.AppendLine("      <th colspan=\"4\" data-col-group=\"AltCover\">AltCover</th>");
    builder.AppendLine("      <th colspan=\"6\" data-col-group=\"Roslyn\">Roslyn</th>");
    builder.AppendLine("      <th colspan=\"2\" data-col-group=\"Sarif\">Sarif</th>");
    builder.AppendLine("    </tr>");
    // Second header row: individual metric names
    builder.AppendLine("    <tr>");
    foreach (var id in _metricOrder)
    {
      builder.AppendLine($"      <th data-col=\"{id}\" data-metric-id=\"{id}\">{WebUtility.HtmlEncode(MetricDisplayNameProvider.GetDisplayName(id))}</th>");
    }
    builder.AppendLine("    </tr>");
    builder.AppendLine("  </thead>");
    builder.AppendLine("  <tbody>");

    // Skip Solution node and render Assemblies directly as top-level items (level 0)
    if (report.Solution is SolutionMetricsNode solution)
    {
      foreach (var assembly in solution.Assemblies.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
      {
        RenderNodeRows(assembly, 0, null, builder, assembly.Name);
      }
    }

    builder.AppendLine("  </tbody>");
    builder.AppendLine("</table>");
    builder.AppendLine("</div>");

    _descendantCountIndex = null;

    return builder.ToString();
  }

  private void RenderNodeRows(MetricsNode node, int level, string? parentId, StringBuilder builder, string? currentAssembly = null, string? currentType = null)
  {
    var thisId = "node-" + (++_idCounter).ToString();
    var hasChildren = HasChildren(node);
    var role = GetNodeRole(node);
    var isStructuralNode = role is "assembly" or "namespace" or "type";
    var assemblyName = UpdateAssemblyName(node, currentAssembly);
    var typeName = UpdateTypeName(node, currentType);
    var isNodeRow = hasChildren || isStructuralNode;
    var symbolTag = isNodeRow ? "th" : "td";
    var rowClass = isNodeRow ? "node-row node-header" : "node-row node-item";
    var symbolTooltipData = BuildSymbolTooltipData(node);
    var coverageLink = BuildCoverageLink(node, isNodeRow, assemblyName);
    var nameText = WebUtility.HtmlEncode(node.Name);

    var sourceDataAttributes = BuildSourceDataAttributes(node);
    var rowStateAttributes = BuildRowStateAttributes(CalculateRowState(node));
    var filterAttributes = BuildFilterAttributes(node);
    var virtualizationAttributes = BuildDescendantAttribute(node);
    var defaultVisibilityAttributes = BuildVisibilityAttributes();
    var combinedAttributes = string.Concat(
      sourceDataAttributes,
      rowStateAttributes,
      filterAttributes,
      virtualizationAttributes,
      defaultVisibilityAttributes);
    AppendRowStart(builder, rowClass, thisId, level, parentId, hasChildren, role, node.IsNew, node.FullyQualifiedName, combinedAttributes);
    AppendSymbolCell(builder, node, symbolTag, hasChildren, isStructuralNode, nameText, coverageLink, thisId, isNodeRow, symbolTooltipData);
    AppendMetricCells(node, symbolTag, builder);
    builder.AppendLine("    </tr>");

    RenderChildren(node, level, thisId, builder, assemblyName, typeName);
  }

  private static bool HasChildren(MetricsNode node)
      => node switch
      {
        SolutionMetricsNode s => s.Assemblies.Any(),
        AssemblyMetricsNode a => a.Namespaces.Any(),
        NamespaceMetricsNode n => n.Types.Any(),
        TypeMetricsNode t => t.Members.Any(),
        _ => false
      };

  private static string? UpdateAssemblyName(MetricsNode node, string? currentAssembly)
      => node is AssemblyMetricsNode assemblyNode ? assemblyNode.Name : currentAssembly;

  private static string? UpdateTypeName(MetricsNode node, string? currentType)
      => node is TypeMetricsNode typeNode ? typeNode.Name : currentType;

  private static string BuildSymbolTooltipData(MetricsNode node)
  {
    if (string.IsNullOrWhiteSpace(node.FullyQualifiedName))
    {
      return string.Empty;
    }

    var role = NodeKindProvider.GetKind(node);
    var roleUpper = role.ToUpperInvariant();
    var source = node.Source;
    var data = new
    {
      role = roleUpper,
      fullyQualifiedName = node.FullyQualifiedName,
      sourcePath = source?.Path,
      sourceStartLine = source?.StartLine,
      sourceEndLine = source?.EndLine
    };

    var json = System.Text.Json.JsonSerializer.Serialize(
        data,
        new System.Text.Json.JsonSerializerOptions
        {
          PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
          DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

    return $" data-symbol-info=\"{WebUtility.HtmlEncode(json)}\"";
  }

  private string? BuildCoverageLink(MetricsNode node, bool isNodeRow, string? assemblyName)
      => isNodeRow && node is TypeMetricsNode typeNode && _coverageLinkBuilder is not null
          ? _coverageLinkBuilder.BuildLink(typeNode, assemblyName)
          : null;

  private static void AppendRowStart(
      StringBuilder builder,
      string rowClass,
      string thisId,
      int level,
      string? parentId,
      bool hasChildren,
      string role,
      bool isNew,
      string? fullyQualifiedName,
      string extendedAttributes)
  {
    var fqnAttribute = string.IsNullOrWhiteSpace(fullyQualifiedName)
        ? string.Empty
        : $" data-fqn=\"{WebUtility.HtmlEncode(fullyQualifiedName)}\"";

    builder.AppendLine("    <tr class=\"" + rowClass + "\" " +
        $"data-id=\"{thisId}\" data-level=\"{level}\" data-parent=\"{parentId ?? string.Empty}\" data-has-children=\"{hasChildren.ToString().ToLowerInvariant()}\" data-role=\"{role}\" data-is-new=\"{(isNew ? "true" : "false")}\"{fqnAttribute}{extendedAttributes}>");
  }

  private static void AppendSymbolCell(
      StringBuilder builder,
      MetricsNode node,
      string symbolTag,
      bool hasChildren,
      bool isStructuralNode,
      string nameText,
      string? coverageLink,
      string thisId,
      bool isNodeRow,
      string nameTooltipData)
  {
    var symbolClasses = "symbol" + (hasChildren || isStructuralNode ? " has-expander" : string.Empty);
    builder.Append($"      <{symbolTag} class=\"{symbolClasses}\">");
    if (hasChildren)
    {
      builder.Append($"<button class=\"expander\" data-target=\"{thisId}\" aria-label=\"Toggle expand/collapse\">-</button>");
    }
    else if (isStructuralNode)
    {
      builder.Append("<span class=\"expander-placeholder symbol-indicator\" data-simple-tooltip=\"No child nodes available\" aria-hidden=\"true\">Ø</span>");
    }

    RenderNodeName(builder, node, nameText, coverageLink, isNodeRow, nameTooltipData);
    AppendRowActionIcons(builder, node);

    builder.AppendLine($"</{symbolTag}>");
  }

  private static void RenderNodeName(
      StringBuilder builder,
      MetricsNode node,
      string nameText,
      string? coverageLink,
      bool isNodeRow,
      string nameTooltipData)
  {
    if (!isNodeRow && node is MemberMetricsNode memberNode)
    {
      if (memberNode.IncludesIteratorStateMachineCoverage)
      {
      builder.Append("<span class=\"method-state-machine symbol-indicator\" data-simple-tooltip=\"Includes coverage from compiler-generated iterator state machine\">⊃</span>");
      }

      builder.Append("<span class=\"name-text item-name\"" + nameTooltipData + ">" + nameText + "</span>");
    }
    else if (isNodeRow && node is TypeMetricsNode)
    {
      if (!string.IsNullOrEmpty(coverageLink))
      {
        builder.Append($"<a href=\"{coverageLink}\" class=\"name-text coverage-link-type\" target=\"_blank\" rel=\"noopener noreferrer\"" + nameTooltipData + ">" + nameText + "</a>");
      }
      else
      {
        builder.Append("<span class=\"name-text\"" + nameTooltipData + ">" + nameText + "</span>");
      }
    }
    else if (!isNodeRow)
    {
      builder.Append("<span class=\"name-text item-name\"" + nameTooltipData + ">" + nameText + "</span>");
    }
    else
    {
      builder.Append("<span class=\"name-text\"" + nameTooltipData + ">" + nameText + "</span>");
    }

    if (node.IsNew)
    {
      builder.Append(" <span class=\"badge badge-new\">NEW</span>");
    }
  }

  private static void AppendRowActionIcons(StringBuilder builder, MetricsNode node)
  {
    builder.AppendLine("      <span class=\"row-action-icons\" aria-hidden=\"true\">");
    if (HasOpenSource(node))
    {
      builder.AppendLine("        <button type=\"button\" class=\"row-action-icon\" data-action=\"open\" aria-label=\"Open file in Cursor\" data-simple-tooltip=\"Open file in Cursor\">");
      builder.AppendLine("          O");
      builder.AppendLine("        </button>");
    }
    builder.AppendLine("        <button type=\"button\" class=\"row-action-icon\" data-action=\"copy\" aria-label=\"Copy symbol name\" data-simple-tooltip=\"Copy fully qualified symbol name\">");
    builder.AppendLine("          C");
    builder.AppendLine("        </button>");
    builder.AppendLine("        <button type=\"button\" class=\"row-action-icon\" data-action=\"filter\" aria-label=\"Filter by this symbol\" data-simple-tooltip=\"Set filter to this symbol\">");
    builder.AppendLine("          F");
    builder.AppendLine("        </button>");
    builder.AppendLine("      </span>");
  }

  private static bool HasOpenSource(MetricsNode node)
      => node.Source?.Path is not null && node.Source.StartLine.HasValue;

  private static string BuildSourceDataAttributes(MetricsNode node)
  {
    if (node.Source?.Path is null || !node.Source.StartLine.HasValue)
    {
      return string.Empty;
    }

    var encodedPath = WebUtility.HtmlEncode(node.Source.Path);
    var startLine = node.Source.StartLine.Value.ToString(CultureInfo.InvariantCulture);
    var endLineAttribute = node.Source.EndLine.HasValue
        ? $" data-source-end-line=\"{node.Source.EndLine.Value.ToString(CultureInfo.InvariantCulture)}\""
        : string.Empty;

    return $" data-source-path=\"{encodedPath}\" data-source-line=\"{startLine}\"{endLineAttribute}";
  }

  private static string BuildFilterAttributes(MetricsNode node)
  {
    var filterSource = string.IsNullOrWhiteSpace(node.FullyQualifiedName)
        ? node.Name
        : node.FullyQualifiedName!;

    if (string.IsNullOrWhiteSpace(filterSource))
    {
      return " data-filter-key=\"\"";
    }

    var normalized = filterSource.ToLowerInvariant();
    return $" data-filter-key=\"{WebUtility.HtmlEncode(normalized)}\"";
  }

  private static string BuildVisibilityAttributes()
    => " data-hidden-by-detail=\"false\" data-hidden-by-filter=\"false\" data-hidden-by-awareness=\"false\" data-hidden-by-state=\"false\" data-expanded=\"true\"";

  private string BuildDescendantAttribute(MetricsNode node)
  {
    if (_descendantCountIndex is null || !_descendantCountIndex.TryGetValue(node, out var count) || count <= 0)
    {
      return string.Empty;
    }

    return $" data-descendant-count=\"{count}\"";
  }

  private RowState CalculateRowState(MetricsNode node)
  {
    var hasError = false;
    var hasWarning = false;
    var hasSuppressed = false;
    var hasDelta = false;

    foreach (var metricId in _metricOrder)
    {
      var suppression = TryGetSuppression(node, metricId);
      if (suppression is not null)
      {
        hasSuppressed = true;
        continue;
      }

      if (!node.Metrics.TryGetValue(metricId, out var metricValue) || metricValue is null)
      {
        continue;
      }

      if (!hasDelta && metricValue.Delta.HasValue && metricValue.Delta.Value != 0)
      {
        hasDelta = true;
      }

      switch (metricValue.Status)
      {
        case ThresholdStatus.Error:
          hasError = true;
          break;
        case ThresholdStatus.Warning:
          hasWarning = true;
          break;
      }

      if (hasError && hasWarning && hasSuppressed && hasDelta)
      {
        break;
      }
    }

    return new RowState(hasError, hasWarning, hasSuppressed, hasDelta);
  }

  private static string BuildRowStateAttributes(RowState state)
  {
    var error = state.HasError ? "true" : "false";
    var warning = state.HasWarning ? "true" : "false";
    var suppressed = state.HasSuppressed ? "true" : "false";
    var delta = state.HasDelta ? "true" : "false";
    return $" data-has-error=\"{error}\" data-has-warning=\"{warning}\" data-has-suppressed=\"{suppressed}\" data-has-delta=\"{delta}\"";
  }

  private Dictionary<MetricsNode, int> BuildDescendantCountIndex(MetricsReport report)
  {
    var index = new Dictionary<MetricsNode, int>(MetricsNodeReferenceComparer.Instance);
    if (report.Solution is MetricsNode root)
    {
      PopulateDescendantCounts(root, index);
    }

    return index;
  }

  private static int PopulateDescendantCounts(MetricsNode node, IDictionary<MetricsNode, int> index)
  {
    var total = 0;
    foreach (var child in EnumerateChildren(node))
    {
      var childDescendants = PopulateDescendantCounts(child, index);
      total += 1 + childDescendants;
    }

    index[node] = total;
    return total;
  }

  private static IEnumerable<MetricsNode> EnumerateChildren(MetricsNode node)
  {
    switch (node)
    {
      case SolutionMetricsNode solution when solution.Assemblies is not null:
        foreach (var assembly in solution.Assemblies)
        {
          yield return assembly;
        }

        break;
      case AssemblyMetricsNode assembly when assembly.Namespaces is not null:
        foreach (var ns in assembly.Namespaces)
        {
          yield return ns;
        }

        break;
      case NamespaceMetricsNode @namespace when @namespace.Types is not null:
        foreach (var type in @namespace.Types)
        {
          yield return type;
        }

        break;
      case TypeMetricsNode type when type.Members is not null:
        foreach (var member in type.Members)
        {
          yield return member;
        }

        break;
    }
  }

  private readonly record struct RowState(bool HasError, bool HasWarning, bool HasSuppressed, bool HasDelta);

  private void RenderChildren(
      MetricsNode node,
      int level,
      string thisId,
      StringBuilder builder,
      string? assemblyName,
      string? typeName)
  {
    switch (node)
    {
      case SolutionMetricsNode solution:
        foreach (var assembly in solution.Assemblies.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
        {
          RenderNodeRows(assembly, level + 1, thisId, builder, assembly.Name);
        }
        break;
      case AssemblyMetricsNode assembly:
        foreach (var ns in assembly.Namespaces.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase))
        {
          RenderNodeRows(ns, level + 1, thisId, builder, assemblyName);
        }
        break;
      case NamespaceMetricsNode @namespace:
        foreach (var type in @namespace.Types.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
        {
          RenderNodeRows(type, level + 1, thisId, builder, assemblyName);
        }
        break;
      case TypeMetricsNode type:
        foreach (var member in type.Members.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
        {
          RenderNodeRows(member, level + 1, thisId, builder, assemblyName, typeName);
        }
        break;
    }
  }

  private static string GetNodeRole(MetricsNode node)
      => node switch
      {
        AssemblyMetricsNode => "assembly",
        NamespaceMetricsNode => "namespace",
        TypeMetricsNode => "type",
        MemberMetricsNode => "member",
        _ => "node"
      };

  private void AppendMetricCells(MetricsNode node, string metricTag, StringBuilder builder)
  {
    foreach (var mid in _metricOrder)
    {
      node.Metrics.TryGetValue(mid, out var val);
      _metricUnits.TryGetValue(mid, out var unit);
      var status = val is null ? "na" : val.Status.ToString().ToLowerInvariant();
      var hasDelta = val is not null && val.Delta.HasValue && val.Delta.Value != 0;
      var suppression = TryGetSuppression(node, mid);
      var suppressedAttr = suppression is null ? string.Empty : " data-suppressed=\"true\"";
      var suppressionDataAttr = BuildSuppressionDataAttribute(suppression);
      var breakdownAttr = BuildBreakdownDataAttribute(mid, val);
      builder.AppendLine($"      <{metricTag} class=\"metric\" data-col=\"{mid}\" data-status=\"{status}\" data-has-delta=\"{(hasDelta ? "true" : "false")}\" data-metric-id=\"{mid}\"{suppressedAttr}{suppressionDataAttr}{breakdownAttr}>{MetricValueRenderer.Render(val, unit)}</{metricTag}>");
    }
  }

  /// <summary>
  /// Builds a data-breakdown attribute for SARIF metrics with non-zero values and breakdown data.
  /// </summary>
  /// <param name="metricId">The metric identifier.</param>
  /// <param name="value">The metric value, may be <see langword="null"/>.</param>
  /// <returns>
  /// A data-breakdown attribute string with JSON-encoded breakdown data, or an empty string
  /// if the metric is not a SARIF metric, has no value, or has no breakdown data.
  /// </returns>
  private static string BuildBreakdownDataAttribute(MetricIdentifier metricId, MetricValue? value)
  {
    // Only add breakdown for SARIF metrics
    if (metricId != MetricIdentifier.SarifCaRuleViolations && metricId != MetricIdentifier.SarifIdeRuleViolations)
    {
      return string.Empty;
    }

    // Only add breakdown if value is non-zero and breakdown exists
    if (value is null || !value.Value.HasValue || value.Value.Value == 0 || value.Breakdown is null || value.Breakdown.Count == 0)
    {
      return string.Empty;
    }

    // Serialize breakdown to JSON and HTML-encode it
    var json = System.Text.Json.JsonSerializer.Serialize(value.Breakdown);
    var encoded = WebUtility.HtmlEncode(json);
    return $" data-breakdown=\"{encoded}\"";
  }

  private Dictionary<(string Fqn, MetricIdentifier Metric), SuppressedSymbolInfo> BuildSuppressedIndex(MetricsReport report)
  {
    var result = new Dictionary<(string Fqn, MetricIdentifier Metric), SuppressedSymbolInfo>();
    foreach (var entry in report.Metadata.SuppressedSymbols)
    {
      if (string.IsNullOrWhiteSpace(entry.FullyQualifiedName) || string.IsNullOrWhiteSpace(entry.Metric))
      {
        continue;
      }

      if (!Enum.TryParse<MetricIdentifier>(entry.Metric, out var metricIdentifier))
      {
        continue;
      }

      var key = (entry.FullyQualifiedName, metricIdentifier);
      // Last-in-wins is acceptable here: multiple suppressions for the same
      // symbol/metric pair are rare and the most recent justification is likely
      // the one users care about.
      result[key] = entry;
    }

    return result;
  }

  private SuppressedSymbolInfo? TryGetSuppression(MetricsNode node, MetricIdentifier metric)
  {
    if (_suppressedIndex is null)
    {
      return null;
    }

    if (string.IsNullOrWhiteSpace(node.FullyQualifiedName))
    {
      return null;
    }

    return _suppressedIndex.TryGetValue((node.FullyQualifiedName, metric), out var info) ? info : null;
  }

  private static string BuildSuppressionDataAttribute(SuppressedSymbolInfo? suppression)
  {
    if (suppression is null)
    {
      return string.Empty;
    }

    var formattedJustification = FormatJustificationText(suppression.Justification);
    var data = new
    {
      ruleId = suppression.RuleId,
      justification = formattedJustification
    };

    var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
    {
      PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    });

    return $" data-suppression-info=\"{WebUtility.HtmlEncode(json)}\"";
  }

  private static string FormatJustificationText(string? justification)
  {
    if (string.IsNullOrWhiteSpace(justification))
    {
      return "Suppressed via SuppressMessage.";
    }

    // WHY: Format justification text for better readability:
    // - Preserve paragraph breaks (split on double newlines)
    // - Escape HTML to prevent XSS

    var text = justification.Trim();
    var parts = new System.Collections.Generic.List<string>();

    // Split by double newlines to preserve paragraph structure
    var paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n", "\r\r" }, StringSplitOptions.RemoveEmptyEntries);

    foreach (var paragraph in paragraphs)
    {
      var escaped = WebUtility.HtmlEncode(paragraph.Trim());
      parts.Add(escaped);
    }

    return string.Join("<br/><br/>", parts);
  }

  private sealed class MetricsNodeReferenceComparer : IEqualityComparer<MetricsNode>
  {
    public static MetricsNodeReferenceComparer Instance { get; } = new();

    public bool Equals(MetricsNode? x, MetricsNode? y)
      => ReferenceEquals(x, y);

    public int GetHashCode(MetricsNode obj)
      => RuntimeHelpers.GetHashCode(obj);
  }
}

