namespace Rca.Tools.MetricsReporter.Rendering;

using System.Net;
using System.Text;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Generates the HTML header section for the metrics report.
/// Includes title, metadata, legend, and action controls.
/// </summary>
internal static class HtmlHeaderGenerator
{
    /// <summary>
    /// Generates the HTML header section with title, metadata, legend, and controls.
    /// </summary>
    /// <param name="report">The metrics report.</param>
    /// <returns>HTML markup for the header section.</returns>
    public static string Generate(MetricsReport report)
    {
        var builder = new StringBuilder();
        
        // Title
        builder.AppendLine($"<h1>Metrics Report for {WebUtility.HtmlEncode(report.Solution.Name)}</h1>");
        
        // Metadata
        builder.AppendLine("<section class=\"meta\"> ");
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

        // Legend
        builder.AppendLine("<section class=\"legend\"> ");
        builder.AppendLine("  <span class=\"badge status-success\">Success</span>");
        builder.AppendLine("  <span class=\"badge status-warning\">Warning</span>");
        builder.AppendLine("  <span class=\"badge status-error\">Error</span>");
        builder.AppendLine("  <span class=\"badge status-na\">N/A</span>");
        builder.AppendLine("  <span class=\"badge badge-new\">NEW</span>");
        builder.AppendLine("</section>");

        // Action buttons and width control
        builder.AppendLine("<div class=\"table-actions\"> ");
        builder.AppendLine("  <label style=\"margin-right:8px;font-size:12px;line-height:28px\">Column width:</label>");
        builder.AppendLine("  <input id=\"symbol-width-slider\" type=\"range\" min=\"240\" max=\"800\" value=\"420\" style=\"width:120px;margin-right:8px\" />");
        builder.AppendLine("  <span id=\"symbol-width-display\" style=\"font-size:12px;min-width:50px;display:inline-block\">420px</span>");
        builder.AppendLine("  <button id=\"reset-width\" style=\"margin-left:6px\">Reset</button>");
        builder.AppendLine("  <div style=\"flex:1\"></div>");
        builder.AppendLine("  <button id=\"expand-all\">Expand all</button>");
        builder.AppendLine("  <button id=\"collapse-all\">Collapse all</button>");
        builder.AppendLine("</div>");

        return builder.ToString();
    }
}

