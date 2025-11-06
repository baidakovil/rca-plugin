namespace Rca.Tools.MetricsReporter.Model;

using System;
using System.Collections.Generic;

/// <summary>
/// Metadata attached to the metrics report.
/// </summary>
public sealed class ReportMetadata
{
    /// <summary>
    /// Report generation timestamp in UTC.
    /// </summary>
    public DateTime GeneratedAtUtc { get; init; }
        = DateTime.UtcNow;

    /// <summary>
    /// Optional reference describing the baseline source (for example, git commit).
    /// </summary>
    public string? BaselineReference { get; init; }
        = null;

    /// <summary>
    /// Paths to the main artefacts.
    /// </summary>
    public ReportPaths Paths { get; init; } = new();

    /// <summary>
    /// Threshold definitions for each metric.
    /// </summary>
    public IDictionary<MetricIdentifier, MetricThreshold> Thresholds { get; init; }
        = new Dictionary<MetricIdentifier, MetricThreshold>();
}

