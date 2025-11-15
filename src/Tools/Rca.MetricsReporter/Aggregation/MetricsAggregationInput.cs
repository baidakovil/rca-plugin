namespace Rca.Tools.MetricsReporter.Aggregation;

using System.Collections.Generic;
using Rca.Tools.MetricsReporter.Model;
using Rca.Tools.MetricsReporter.Processing;

/// <summary>
/// Data required to build the consolidated metrics report.
/// </summary>
public sealed class MetricsAggregationInput
{
    /// <summary>
    /// Solution name displayed in the report.
    /// </summary>
    public string SolutionName { get; init; } = "UnknownSolution";

    /// <summary>
    /// AltCover/OpenCover documents.
    /// </summary>
    public IList<ParsedMetricsDocument> AltCoverDocuments { get; init; } = new List<ParsedMetricsDocument>();

    /// <summary>
    /// Roslyn code metrics documents.
    /// </summary>
    public IList<ParsedMetricsDocument> RoslynDocuments { get; init; } = new List<ParsedMetricsDocument>();

    /// <summary>
    /// SARIF documents.
    /// </summary>
    public IList<ParsedMetricsDocument> SarifDocuments { get; init; } = new List<ParsedMetricsDocument>();

    /// <summary>
    /// Baseline report used to compute deltas. Can be <see langword="null"/>.
    /// </summary>
    public MetricsReport? Baseline { get; init; }

    /// <summary>
    /// Metric thresholds grouped by symbol level.
    /// </summary>
    public IDictionary<MetricIdentifier, MetricThresholdDefinition> Thresholds { get; init; }
        = new Dictionary<MetricIdentifier, MetricThresholdDefinition>();

    /// <summary>
    /// Paths to the key artefacts.
    /// </summary>
    public ReportPaths Paths { get; init; } = new();

    /// <summary>
    /// Optional textual description of the baseline (for example, git commit hash).
    /// </summary>
    public string? BaselineReference { get; init; }
}

