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
        // Convert UTC time to local time for display
        // WHY: Users expect to see time in their local timezone, not UTC, for better readability
        var localTime = report.Metadata.GeneratedAtUtc.ToLocalTime();
        builder.AppendLine($"  <p><strong>Generated at:</strong> {localTime:yyyy-MM-dd HH:mm:ss}</p>");
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

        return builder.ToString();
    }
}






