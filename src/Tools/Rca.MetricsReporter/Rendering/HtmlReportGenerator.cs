namespace Rca.Tools.MetricsReporter.Rendering;

using System.Net;
using System.Text;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Generates the HTML dashboard for the metrics report.
/// Coordinates the generation of HTML header, table, styles, and scripts.
/// </summary>
public sealed class HtmlReportGenerator
{
    // Explicit ordering required by the user: altcover -> roslyn -> sarif
    private static readonly MetricIdentifier[] MetricOrder = new[]
    {
        // AltCover: sequence, branch, npath, cyclomatic (cyclomatic placed last in altcover group)
        MetricIdentifier.AltCoverSequenceCoverage,
        MetricIdentifier.AltCoverBranchCoverage,
        MetricIdentifier.AltCoverNPathComplexity,
        MetricIdentifier.AltCoverCyclomaticComplexity,

        // Roslyn: cyclomatic first in roslyn group, then maintainability, coupling, depth, source lines, executable lines
        MetricIdentifier.RoslynCyclomaticComplexity,
        MetricIdentifier.RoslynMaintainabilityIndex,
        MetricIdentifier.RoslynClassCoupling,
        MetricIdentifier.RoslynDepthOfInheritance,
        MetricIdentifier.RoslynSourceLines,
        MetricIdentifier.RoslynExecutableLines,

        // SARIF
        MetricIdentifier.SarifCaRuleViolations,
        MetricIdentifier.SarifIdeRuleViolations
    };

    /// <summary>
    /// Produces HTML markup for the specified report.
    /// The layout is a minimalistic table similar to Visual Studio Code Code Metrics Results.
    /// </summary>
    /// <param name="report">The metrics report to generate HTML for.</param>
    /// <returns>Complete HTML document as a string.</returns>
    public string Generate(MetricsReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var builder = new StringBuilder();
        
        // HTML document structure
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"en\"> ");
        builder.AppendLine("<head>");
        builder.AppendLine("  <meta charset=\"utf-8\"/>");
        builder.AppendLine($"  <title>Metrics Report - {WebUtility.HtmlEncode(report.Solution.Name)}</title>");
        builder.AppendLine("  <style>");
        builder.AppendLine(HtmlStylesGenerator.Generate());
        builder.AppendLine("  </style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");

        // Header section (title, metadata, legend, controls)
        builder.Append(HtmlHeaderGenerator.Generate(report));

        // Table section
        var tableGenerator = new HtmlTableGenerator(MetricOrder);
        builder.Append(tableGenerator.Generate(report));

        // JavaScript section
        builder.AppendLine("<script>");
        builder.AppendLine(HtmlScriptGenerator.Generate());
        builder.AppendLine("</script>");

        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }
}

