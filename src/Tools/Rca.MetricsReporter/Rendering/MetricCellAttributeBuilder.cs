namespace Rca.Tools.MetricsReporter.Rendering;

using System.Collections.Generic;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Builds HTML attributes for metric cells.
/// </summary>
internal sealed class MetricCellAttributeBuilder
{
  private readonly Dictionary<(string Fqn, MetricIdentifier Metric), SuppressedSymbolInfo>? _suppressedIndex;

  /// <summary>
  /// Initializes a new instance of the <see cref="MetricCellAttributeBuilder"/> class.
  /// </summary>
  /// <param name="suppressedIndex">Optional index of suppressed symbols for lookup.</param>
  public MetricCellAttributeBuilder(Dictionary<(string Fqn, MetricIdentifier Metric), SuppressedSymbolInfo>? suppressedIndex)
  {
    _suppressedIndex = suppressedIndex;
  }

  /// <summary>
  /// Builds all HTML attributes for a metric cell.
  /// </summary>
  /// <param name="node">The metrics node.</param>
  /// <param name="metricId">The metric identifier.</param>
  /// <param name="value">The metric value, may be <see langword="null"/>.</param>
  /// <returns>A tuple containing the status, hasDelta flag, suppressed attribute, suppression data attribute, and breakdown attribute.</returns>
  public (string Status, bool HasDelta, string SuppressedAttr, string SuppressionDataAttr, string BreakdownAttr) BuildAttributes(
    MetricsNode node,
    MetricIdentifier metricId,
    MetricValue? value)
  {
    var status = value is null ? "na" : value.Status.ToString().ToLowerInvariant();
    var hasDelta = value is not null && value.Delta.HasValue && value.Delta.Value != 0;
    var suppression = TryGetSuppression(node, metricId);
    var suppressedAttr = suppression is null ? string.Empty : " data-suppressed=\"true\"";
    var suppressionDataAttr = SuppressionAttributeBuilder.BuildDataAttribute(suppression);
    var breakdownAttr = BreakdownAttributeBuilder.BuildDataAttribute(metricId, value);

    return (status, hasDelta, suppressedAttr, suppressionDataAttr, breakdownAttr);
  }

  private SuppressedSymbolInfo? TryGetSuppression(MetricsNode node, MetricIdentifier metric)
  {
    if (_suppressedIndex is null)
    {
      return null;
    }

    if (string.IsNullOrWhiteSpace(node.FullyQualifiedName))
    {
      return null;
    }

    return _suppressedIndex.TryGetValue((node.FullyQualifiedName, metric), out var info) ? info : null;
  }
}

