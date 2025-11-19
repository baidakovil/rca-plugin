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
  /// Threshold definitions grouped by symbol level.
  /// </summary>
  public IDictionary<MetricIdentifier, IDictionary<MetricSymbolLevel, MetricThreshold>> ThresholdsByLevel { get; init; }
      = new Dictionary<MetricIdentifier, IDictionary<MetricSymbolLevel, MetricThreshold>>();

  /// <summary>
  /// Metric descriptions sourced from the thresholds definition.
  /// </summary>
  public IDictionary<MetricIdentifier, string?> ThresholdDescriptions { get; init; }
      = new Dictionary<MetricIdentifier, string?>();

  /// <summary>
  /// Comma-separated list of excluded member name patterns used when generating this report.
  /// </summary>
  /// <remarks>
  /// This property stores the list of member name patterns that were excluded from the metrics report.
  /// It is used for display purposes in the HTML report header.
  /// </remarks>
  public string? ExcludedMemberNamesPatterns { get; init; }

  /// <summary>
  /// Comma-separated list of excluded assembly name patterns used when generating this report.
  /// </summary>
  /// <remarks>
  /// This property stores the list of assembly name patterns that were excluded from the metrics report.
  /// It is used for display purposes in the HTML report header.
  /// </remarks>
  public string? ExcludedAssemblyNames { get; init; }

  /// <summary>
  /// Comma-separated list of type name patterns that were excluded from the metrics report.
  /// </summary>
  /// <remarks>
  /// Patterns are matched against fully qualified type names using substring matching.
  /// This property is used for display purposes in the HTML report header.
  /// </remarks>
  public string? ExcludedTypeNamePatterns { get; init; }
}

