namespace Rca.Tools.MetricsReporter.Rendering;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Генерирует HTML-дашборд для отчёта по метрикам.
/// </summary>
public sealed class HtmlReportGenerator
{
    private static readonly MetricIdentifier[] MetricOrder = Enum.GetValues<MetricIdentifier>();

    /// <summary>
    /// Генерирует HTML-контент для указанного отчёта.
    /// </summary>
    /// <param name="report">Отчёт по метрикам.</param>
    /// <returns>HTML-документ.</returns>
    public string Generate(MetricsReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("  <meta charset=\"utf-8\"/>");
        builder.AppendLine($"  <title>Metrics Report - {WebUtility.HtmlEncode(report.Solution.Name)}</title>");
        builder.AppendLine("  <style>");
        builder.AppendLine(GetStyles());
        builder.AppendLine("  </style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine($"<h1>Metrics Report for {WebUtility.HtmlEncode(report.Solution.Name)}</h1>");
        builder.AppendLine("<section class=\"meta\">");
        builder.AppendLine($"  <p><strong>Generated at:</strong> {report.Metadata.GeneratedAtUtc:u}</p>");
        if (!string.IsNullOrWhiteSpace(report.Metadata.BaselineReference))
        {
            builder.AppendLine($"  <p><strong>Baseline:</strong> {WebUtility.HtmlEncode(report.Metadata.BaselineReference)}</p>");
        }
        builder.AppendLine($"  <p><strong>Metrics JSON:</strong> {WebUtility.HtmlEncode(report.Metadata.Paths.Report)}</p>");
        if (!string.IsNullOrWhiteSpace(report.Metadata.Paths.Baseline))
        {
            builder.AppendLine($"  <p><strong>Baseline JSON:</strong> {WebUtility.HtmlEncode(report.Metadata.Paths.Baseline)}</p>");
        }
        builder.AppendLine("</section>");

        builder.AppendLine("<section class=\"legend\">");
        builder.AppendLine("  <span class=\"badge status-success\">Success</span>");
        builder.AppendLine("  <span class=\"badge status-warning\">Warning</span>");
        builder.AppendLine("  <span class=\"badge status-error\">Error</span>");
        builder.AppendLine("  <span class=\"badge status-na\">N/A</span>");
        builder.AppendLine("  <span class=\"badge badge-new\">NEW</span>");
        builder.AppendLine("</section>");

        RenderNode(builder, report.Solution);

        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }

    private void RenderNode(StringBuilder builder, MetricsNode node)
    {
        var worstStatus = DetermineWorstStatus(node);
        var statusClass = $"status-{worstStatus.ToString().ToLowerInvariant()}";

        builder.AppendLine("<details open class=\"node\">");
        builder.Append("<summary>");
        builder.Append($"<span class=\"badge {statusClass}\">{worstStatus}</span> ");
        builder.Append(WebUtility.HtmlEncode(node.Name));

        if (node.IsNew)
        {
            builder.Append(" <span class=\"badge badge-new\">NEW</span>");
        }

        if (!string.IsNullOrWhiteSpace(node.FullyQualifiedName))
        {
            builder.Append($" <span class=\"fqn\">({WebUtility.HtmlEncode(node.FullyQualifiedName)})</span>");
        }

        if (node.Source?.Path is not null && node.Source.StartLine.HasValue)
        {
            builder.Append($" <span class=\"source\">[{WebUtility.HtmlEncode(node.Source.Path)}:{node.Source.StartLine}]</span>");
        }

        builder.AppendLine("</summary>");

        RenderMetricsTable(builder, node.Metrics);

        switch (node)
        {
            case SolutionMetricsNode solution:
                foreach (var assembly in solution.Assemblies.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
                {
                    RenderNode(builder, assembly);
                }

                break;
            case AssemblyMetricsNode assembly:
                foreach (var ns in assembly.Namespaces.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase))
                {
                    RenderNode(builder, ns);
                }

                break;
            case NamespaceMetricsNode @namespace:
                foreach (var type in @namespace.Types.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
                {
                    RenderNode(builder, type);
                }

                break;
            case TypeMetricsNode type:
                foreach (var member in type.Members.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
                {
                    RenderNode(builder, member);
                }

                break;
        }

    builder.AppendLine("</details>");
    }

    private static void RenderMetricsTable(StringBuilder builder, IDictionary<MetricIdentifier, MetricValue> metrics)
    {
        builder.AppendLine("<table class=\"metrics\">");
        builder.AppendLine("  <thead>");
        builder.AppendLine("    <tr>");
        foreach (var identifier in MetricOrder)
        {
            builder.AppendLine($"      <th>{WebUtility.HtmlEncode(GetMetricDisplayName(identifier))}</th>");
        }

        builder.AppendLine("    </tr>");
        builder.AppendLine("  </thead>");
        builder.AppendLine("  <tbody>");
        builder.AppendLine("    <tr>");
        foreach (var identifier in MetricOrder)
        {
            metrics.TryGetValue(identifier, out var value);
            builder.Append("      <td>");
            builder.Append(RenderMetricValue(value));
            builder.AppendLine("</td>");
        }

        builder.AppendLine("    </tr>");
        builder.AppendLine("  </tbody>");
        builder.AppendLine("</table>");
    }

    private static string RenderMetricValue(MetricValue? value)
    {
        if (value is null)
        {
            return "<span class=\"metric-value status-na\">-</span>";
        }

        var statusClass = $"status-{value.Status.ToString().ToLowerInvariant()}";
        var displayValue = value.Value.HasValue
            ? FormatValue(value.Value.Value, value.Unit)
            : "-";

        var builder = new StringBuilder();
        builder.Append($"<span class=\"metric-value {statusClass}\">{displayValue}</span>");

        if (value.Delta.HasValue && value.Delta.Value != 0)
        {
            var deltaText = value.Delta.Value > 0 ? $"+{value.Delta.Value:0.##}" : $"{value.Delta.Value:0.##}";
            var deltaClass = value.Delta.Value >= 0 ? "delta-positive" : "delta-negative";
            builder.Append($"<sup class=\"{deltaClass}\">{deltaText}</sup>");
        }

        return builder.ToString();
    }

    private static string FormatValue(decimal value, string? unit)
        => unit switch
        {
            "percent" => $"{value:0.##}%",
            _ => $"{value:0.##}"
        };

    private static ThresholdStatus DetermineWorstStatus(MetricsNode node)
    {
        var severity = ThresholdStatus.NotApplicable;
        foreach (var metric in node.Metrics.Values)
        {
            if (metric.Status == ThresholdStatus.NotApplicable)
            {
                continue;
            }

            if (GetSeverity(metric.Status) > GetSeverity(severity))
            {
                severity = metric.Status;
            }
        }

        return severity;
    }

    private static int GetSeverity(ThresholdStatus status)
        => status switch
        {
            ThresholdStatus.Error => 3,
            ThresholdStatus.Warning => 2,
            ThresholdStatus.Success => 1,
            _ => 0
        };

    private static string GetMetricDisplayName(MetricIdentifier identifier)
        => identifier switch
        {
            MetricIdentifier.AltCoverSequenceCoverage => "Sequence Coverage (AltCover)",
            MetricIdentifier.AltCoverBranchCoverage => "Branch Coverage (AltCover)",
            MetricIdentifier.AltCoverCyclomaticComplexity => "Cyclomatic (AltCover)",
            MetricIdentifier.AltCoverNPathComplexity => "NPath (AltCover)",
            MetricIdentifier.RoslynMaintainabilityIndex => "Maintainability (Roslyn)",
            MetricIdentifier.RoslynCyclomaticComplexity => "Cyclomatic (Roslyn)",
            MetricIdentifier.RoslynClassCoupling => "Class Coupling",
            MetricIdentifier.RoslynDepthOfInheritance => "Depth of Inheritance",
            MetricIdentifier.RoslynSourceLines => "Source Lines",
            MetricIdentifier.RoslynExecutableLines => "Executable Lines",
            MetricIdentifier.SarifCaRuleViolations => "CA Violations",
            MetricIdentifier.SarifIdeRuleViolations => "IDE Violations",
            _ => identifier.ToString()
        };

    private static string GetStyles() => @"
:root {
  color-scheme: light dark;
  font-family: 'Segoe UI', sans-serif;
  font-size: 14px;
  line-height: 1.4;
}
body {
  margin: 16px;
}
h1 {
  margin-bottom: 0;
}
.meta p {
  margin: 2px 0;
}
.legend {
  margin: 12px 0 20px;
  display: flex;
  gap: 8px;
}
.badge {
  display: inline-flex;
  align-items: center;
  padding: 2px 6px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 600;
  text-transform: uppercase;
}
.badge-new {
  background-color: #1a7f37;
  color: #ffffff;
}
.status-success {
  background-color: #1a7f37;
  color: #ffffff;
}
.status-warning {
  background-color: #f0ad4e;
  color: #000000;
}
.status-error {
  background-color: #d9534f;
  color: #ffffff;
}
.status-notapplicable, .status-na {
  background-color: #6c757d;
  color: #ffffff;
}
.node {
  margin-left: 12px;
  border-left: 2px solid rgba(128, 128, 128, 0.3);
  padding-left: 12px;
}
.node > summary {
  cursor: pointer;
  margin: 8px 0;
  list-style: none;
}
.node > summary::-webkit-details-marker {
  display: none;
}
.metrics {
  width: 100%;
  border-collapse: collapse;
  margin-bottom: 12px;
}
.metrics th, .metrics td {
  border: 1px solid rgba(128, 128, 128, 0.3);
  padding: 4px 6px;
  text-align: left;
  vertical-align: top;
}
.metrics th {
  background-color: rgba(128, 128, 128, 0.15);
  position: sticky;
  top: 0;
  z-index: 1;
}
.metric-value {
  font-weight: 600;
}
.delta-positive {
  color: #1a7f37;
  margin-left: 4px;
}
.delta-negative {
  color: #d9534f;
  margin-left: 4px;
}
.fqn, .source {
  font-family: 'Consolas', 'Courier New', monospace;
  font-size: 12px;
  color: rgba(128, 128, 128, 0.8);
}";
}

