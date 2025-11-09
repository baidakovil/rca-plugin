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

        return builder.ToString();
    }
}






