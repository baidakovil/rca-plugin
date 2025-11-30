namespace Rca.Tools.MetricsReporter.Aggregation;

using System;
using System.Collections.Generic;
using Rca.Tools.MetricsReporter.Model;

internal sealed class SuppressedMetricResolver
{
  private static readonly MetricIdentifier[] FallbackMetrics =
  {
    MetricIdentifier.SarifIdeRuleViolations,
    MetricIdentifier.SarifCaRuleViolations
  };

  public bool TryResolve(MetricsNode node, string? ruleId, out MetricIdentifier metricIdentifier)
  {
    if (node is null)
    {
      throw new ArgumentNullException(nameof(node));
    }

    var preferredMetric = GetPreferredMetric(ruleId);
    if (preferredMetric.HasValue && NodeHasMetric(node, preferredMetric.Value))
    {
      metricIdentifier = preferredMetric.Value;
      return true;
    }

    foreach (var candidate in FallbackMetrics)
    {
      if (NodeHasMetric(node, candidate))
      {
        metricIdentifier = candidate;
        return true;
      }
    }

    metricIdentifier = default;
    return false;
  }

  public static bool IsKnownMetric(string? metricName)
      => !string.IsNullOrWhiteSpace(metricName) &&
         Enum.TryParse<MetricIdentifier>(metricName, ignoreCase: true, out _);

  private static MetricIdentifier? GetPreferredMetric(string? ruleId)
  {
    if (string.IsNullOrWhiteSpace(ruleId))
    {
      return null;
    }

    if (ruleId.StartsWith("IDE", StringComparison.OrdinalIgnoreCase))
    {
      return MetricIdentifier.SarifIdeRuleViolations;
    }

    if (ruleId.StartsWith("CA", StringComparison.OrdinalIgnoreCase))
    {
      return MetricIdentifier.SarifCaRuleViolations;
    }

    return null;
  }

  private static bool NodeHasMetric(MetricsNode node, MetricIdentifier identifier)
      => node.Metrics.TryGetValue(identifier, out var value) && value?.Value is not null;
}

