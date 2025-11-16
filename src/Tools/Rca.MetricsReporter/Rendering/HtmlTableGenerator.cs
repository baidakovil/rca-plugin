namespace Rca.Tools.MetricsReporter.Rendering;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Generates the HTML table for the metrics report.
/// Handles recursive rendering of the metrics hierarchy.
/// </summary>
internal sealed class HtmlTableGenerator
{
    private readonly MetricIdentifier[] _metricOrder;
    private int _idCounter;
    private string? _coverageHtmlDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="HtmlTableGenerator"/> class.
    /// </summary>
    /// <param name="metricOrder">The order of metrics to display in columns.</param>
    public HtmlTableGenerator(MetricIdentifier[] metricOrder)
    {
        _metricOrder = metricOrder ?? throw new ArgumentNullException(nameof(metricOrder));
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
        _coverageHtmlDir = coverageHtmlDir;
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

        return builder.ToString();
    }

    private void RenderNodeRows(MetricsNode node, int level, string? parentId, StringBuilder builder, string? currentAssembly = null, string? currentType = null)
    {
        var thisId = "node-" + (++_idCounter).ToString();

        // Check if node has children
        var hasChildren = node switch
        {
            SolutionMetricsNode s => s.Assemblies.Any(),
            AssemblyMetricsNode a => a.Namespaces.Any(),
            NamespaceMetricsNode n => n.Types.Any(),
            TypeMetricsNode t => t.Members.Any(),
            _ => false
        };

        var role = GetNodeRole(node);
        var isStructuralNode = role is "assembly" or "namespace" or "type";
        
        // Update assembly and type for recursive calls
        var assemblyName = currentAssembly;
        var typeName = currentType;
        if (node is AssemblyMetricsNode assemblyNode)
        {
            assemblyName = assemblyNode.Name;
        }
        else if (node is TypeMetricsNode typeNode)
        {
            typeName = typeNode.Name;
        }

        var kind = NodeKindProvider.GetKind(node);
        var tooltip = string.Empty;
        if (!string.IsNullOrWhiteSpace(node.FullyQualifiedName))
        {
            tooltip = WebUtility.HtmlEncode(node.FullyQualifiedName + " — " + kind);
        }

        // Determine if this is a node row (has children) or a regular row
        var isNodeRow = hasChildren || isStructuralNode;
        var rowClass = isNodeRow ? "node-row node-header" : "node-row node-item";
        
        // Add FQN as data attribute for efficient filtering
        var fqnAttribute = string.IsNullOrWhiteSpace(node.FullyQualifiedName) 
            ? string.Empty 
            : $" data-fqn=\"{WebUtility.HtmlEncode(node.FullyQualifiedName)}\"";
        
        builder.AppendLine("    <tr class=\"" + rowClass + "\" " +
            $"data-id=\"{thisId}\" data-level=\"{level}\" data-parent=\"{parentId ?? string.Empty}\" data-has-children=\"{hasChildren.ToString().ToLowerInvariant()}\" data-role=\"{role}\" data-is-new=\"{(node.IsNew ? "true" : "false")}\"{fqnAttribute}>");

        // Symbol cell with tooltip and class for expander presence
        var symbolClasses = "symbol";
        if (hasChildren || isStructuralNode)
        {
            symbolClasses += " has-expander";
        }

        // For node rows, use <th>, for regular rows use <td>
        var symbolTag = isNodeRow ? "th" : "td";
        
        if (!string.IsNullOrWhiteSpace(tooltip))
        {
            builder.Append($"      <{symbolTag} class=\"{symbolClasses}\" title=\"{tooltip}\">");
        }
        else
        {
            builder.Append($"      <{symbolTag} class=\"{symbolClasses}\">");
        }

        // Expander button (only if node has children)
        // WHY: Default state is expanded, so expander shows '-' initially
        if (hasChildren)
        {
            builder.Append($"<button class=\"expander\" data-target=\"{thisId}\" aria-label=\"Toggle expand/collapse\">-</button>");
        }
        else if (isStructuralNode)
        {
            builder.Append("<span class=\"expander-placeholder\" aria-hidden=\"true\">∅</span>");
        }

        // Name and NEW badge
        var nameText = WebUtility.HtmlEncode(node.Name);
        
        // Create coverage link only for types
        string? coverageLink = null;
        if (isNodeRow && node is TypeMetricsNode)
        {
            coverageLink = BuildCoverageLink(node, assemblyName);
        }
        
        if (!isNodeRow && node is MemberMetricsNode)
        {
            // For member rows, wrap name in a span with red color class
            builder.Append("<span class=\"name-text item-name\">" + nameText + "</span>");
        }
        else if (isNodeRow && node is TypeMetricsNode)
        {
            // For type rows (node rows), create link to coverage HTML if available
            if (!string.IsNullOrEmpty(coverageLink))
            {
                builder.Append($"<a href=\"{coverageLink}\" class=\"name-text coverage-link coverage-link-type\" target=\"_blank\" rel=\"noopener noreferrer\">" + nameText + "</a>");
            }
            else
            {
                // For node rows without link, use plain text
                builder.Append("<span class=\"name-text\">" + nameText + "</span>");
            }
        }
        else if (!isNodeRow)
        {
            // For regular rows (non-member), wrap name in a span with red color class
            builder.Append("<span class=\"name-text item-name\">" + nameText + "</span>");
        }
        else
        {
            // For other node rows (assembly/namespace), use plain text
            builder.Append("<span class=\"name-text\">" + nameText + "</span>");
        }
        
        if (node.IsNew)
        {
            builder.Append(" <span class=\"badge badge-new\">NEW</span>");
        }

        builder.AppendLine($"</{symbolTag}>");

        // Metric cells - use <th> for node rows, <td> for regular rows
        var metricTag = isNodeRow ? "th" : "td";
        AppendMetricCells(node, metricTag, builder);

        builder.AppendLine("    </tr>");

        // Render children
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
            var status = val is null ? "na" : val.Status.ToString().ToLowerInvariant();
            var hasDelta = val is not null && val.Delta.HasValue && val.Delta.Value != 0;
            builder.AppendLine($"      <{metricTag} class=\"metric\" data-col=\"{mid}\" data-status=\"{status}\" data-has-delta=\"{(hasDelta ? "true" : "false")}\" data-metric-id=\"{mid}\">{MetricValueRenderer.Render(val)}</{metricTag}>");
        }
    }

    /// <summary>
    /// Builds a coverage HTML link for a type node if the coverage HTML file exists.
    /// </summary>
    /// <param name="node">The type node to build a link for.</param>
    /// <param name="assemblyName">The assembly name containing the type.</param>
    /// <returns>
    /// A file:// URL to the coverage HTML file, or <see langword="null"/> if the file doesn't exist
    /// or CoverageHtmlDir is not specified.
    /// </returns>
    /// <remarks>
    /// Coverage HTML files are generated by reportgenerator with the naming pattern:
    /// {Assembly}_{Type}.html (e.g., "Rca.Loader_PipeResponseFactory.html").
    /// The type name is taken from node.Name.
    /// Only creates links for types, not for members or assembly/namespace nodes.
    /// </remarks>
    private string? BuildCoverageLink(MetricsNode node, string? assemblyName)
    {
        // Only create links for types
        if (node is not TypeMetricsNode)
        {
            return null;
        }

        // CoverageHtmlDir must be specified and exist
        if (string.IsNullOrWhiteSpace(_coverageHtmlDir) || !Directory.Exists(_coverageHtmlDir))
        {
            return null;
        }

        // Assembly name is required
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return null;
        }

        // Use node.Name as type name
        var effectiveTypeName = node.Name;
        if (string.IsNullOrWhiteSpace(effectiveTypeName))
        {
            return null;
        }

        // Build HTML filename: {Assembly}_{Type}.html
        // Example: "Rca.Loader_PipeResponseFactory.html"
        var htmlFileName = $"{assemblyName}_{effectiveTypeName}.html";
        var htmlFilePath = Path.Combine(_coverageHtmlDir, htmlFileName);

        // Only create link if HTML file exists
        if (!File.Exists(htmlFilePath))
        {
            return null;
        }

        // Convert to file:// URL format
        var fileUri = new Uri(htmlFilePath);
        return WebUtility.HtmlEncode(fileUri.AbsoluteUri);
    }
}

