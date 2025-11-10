namespace Rca.Tools.MetricsReporter.Services;

using System.Collections.Generic;

/// <summary>
/// Describes command-line options supplied to the metrics reporter.
/// </summary>
public sealed class MetricsReporterOptions
{
    /// <summary>
    /// Solution name displayed in the report.
    /// </summary>
    public string SolutionName { get; init; } = "Solution";

    /// <summary>
    /// Path to the AltCover/OpenCover coverage.xml file.
    /// </summary>
    public string? AltCoverPath { get; init; }

    /// <summary>
    /// Paths to Roslyn code metrics XML reports.
    /// </summary>
    public IReadOnlyCollection<string> RoslynPaths { get; init; } = new List<string>();

    /// <summary>
    /// Paths to SARIF files.
    /// </summary>
    public IReadOnlyCollection<string> SarifPaths { get; init; } = new List<string>();

    /// <summary>
    /// Path to the baseline JSON file.
    /// </summary>
    public string? BaselinePath { get; init; }

    /// <summary>
    /// Optional textual descriptor for the baseline (commit hash, build identifier, etc.).
    /// </summary>
    public string? BaselineReference { get; init; }

    /// <summary>
    /// Threshold values encoded as JSON.
    /// </summary>
    public string? ThresholdsJson { get; init; }

    /// <summary>
    /// Path to an existing JSON report to load (for HTML generation only).
    /// </summary>
    public string? InputJsonPath { get; init; }

    /// <summary>
    /// Path to the generated JSON report.
    /// </summary>
    public string OutputJsonPath { get; init; } = string.Empty;

    /// <summary>
    /// Path to the generated HTML report.
    /// </summary>
    public string OutputHtmlPath { get; init; } = string.Empty;

    /// <summary>
    /// Metrics directory (MetricsDir).
    /// </summary>
    public string MetricsDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Path to the metrics reporter log file.
    /// </summary>
    public string LogFilePath { get; init; } = string.Empty;

    /// <summary>
    /// Comma-separated or semicolon-separated list of method names to exclude from metrics reports.
    /// </summary>
    /// <remarks>
    /// Method names are normalized (leading dots are removed). Examples: "ctor,cctor,MoveNext" or "ctor;cctor;MoveNext".
    /// If not specified, default excluded methods are used (constructors and compiler-generated methods).
    /// </remarks>
    public string? ExcludedMethodNames { get; init; }
}

