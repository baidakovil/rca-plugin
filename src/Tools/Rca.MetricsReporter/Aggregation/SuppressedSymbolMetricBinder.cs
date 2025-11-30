namespace Rca.Tools.MetricsReporter.Aggregation;

using System;
using System.Collections.Generic;
using System.Linq;
using Rca.Tools.MetricsReporter.Model;

internal static class SuppressedSymbolMetricBinder
{
  private static readonly MetricIdentifier[] FallbackMetrics =
  {
    MetricIdentifier.SarifIdeRuleViolations,
    MetricIdentifier.SarifCaRuleViolations
  };

  public static void Bind(SolutionMetricsNode solution, IList<SuppressedSymbolInfo> suppressedSymbols)
  {
    if (solution is null)
    {
      throw new ArgumentNullException(nameof(solution));
    }

    if (suppressedSymbols.Count == 0)
    {
      return;
    }

    var index = BuildNodeIndex(solution);

    foreach (var suppressed in suppressedSymbols)
    {
      if (string.IsNullOrWhiteSpace(suppressed.FullyQualifiedName))
      {
        continue;
      }

      if (!index.TryGetValue(suppressed.FullyQualifiedName, out var node))
      {
        continue;
      }

      if (HasRecognizedMetric(suppressed))
      {
        continue;
      }

      var metric = DetermineMetric(node, suppressed.RuleId);
      if (metric is not null)
      {
        suppressed.Metric = metric;
      }
    }
  }

  private static string? DetermineMetric(MetricsNode node, string ruleId)
  {
    var preferred = DeterminePreferredMetric(ruleId);
    if (preferred.HasValue && HasMetric(node, preferred.Value))
    {
      return preferred.Value.ToString();
    }

    foreach (var candidate in FallbackMetrics)
    {
      if (HasMetric(node, candidate))
      {
        return candidate.ToString();
      }
    }

    return null;
  }

  private static MetricIdentifier? DeterminePreferredMetric(string? ruleId)
  {
    if (!string.IsNullOrWhiteSpace(ruleId))
    {
      if (ruleId.StartsWith("IDE", StringComparison.OrdinalIgnoreCase))
      {
        return MetricIdentifier.SarifIdeRuleViolations;
      }

      if (ruleId.StartsWith("CA", StringComparison.OrdinalIgnoreCase))
      {
        return MetricIdentifier.SarifCaRuleViolations;
      }
    }

    return null;
  }

  private static bool HasMetric(MetricsNode node, MetricIdentifier identifier)
      => node.Metrics.TryGetValue(identifier, out var value) && value?.Value is not null;

  private static Dictionary<string, MetricsNode> BuildNodeIndex(SolutionMetricsNode solution)
  {
    var index = new Dictionary<string, MetricsNode>(StringComparer.Ordinal);

    void AddNode(MetricsNode? node)
    {
      if (node is null)
      {
        return;
      }

      if (!string.IsNullOrWhiteSpace(node.FullyQualifiedName))
      {
        index[node.FullyQualifiedName] = node;
      }
    }

    foreach (var assembly in solution.Assemblies)
    {
      AddNode(assembly);
      foreach (var ns in assembly.Namespaces)
      {
        AddNode(ns);
        foreach (var type in ns.Types)
        {
          AddNode(type);
          foreach (var member in type.Members)
          {
            AddNode(member);
          }
        }
      }
    }

    return index;
  }

  private static bool HasRecognizedMetric(SuppressedSymbolInfo suppressed)
  {
    if (suppressed is null)
    {
      throw new ArgumentNullException(nameof(suppressed));
    }

    if (string.IsNullOrWhiteSpace(suppressed.Metric))
    {
      return false;
    }

    return Enum.TryParse<MetricIdentifier>(suppressed.Metric, ignoreCase: true, out _);
  }
}

