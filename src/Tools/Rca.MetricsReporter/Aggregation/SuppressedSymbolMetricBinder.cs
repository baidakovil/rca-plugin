namespace Rca.Tools.MetricsReporter.Aggregation;

using System;
using System.Collections.Generic;
using System.Linq;
using Rca.Tools.MetricsReporter.Model;

internal static class SuppressedSymbolMetricBinder
{
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

    var lookup = MetricsNodeLookup.Create(solution);
    var resolver = new SuppressedMetricResolver();

    foreach (var suppressed in suppressedSymbols)
    {
      if (string.IsNullOrWhiteSpace(suppressed.FullyQualifiedName))
      {
        continue;
      }

      if (SuppressedMetricResolver.IsKnownMetric(suppressed.Metric))
      {
        continue;
      }

      if (!lookup.TryGetNode(suppressed.FullyQualifiedName, out var node))
      {
        continue;
      }

      if (resolver.TryResolve(node, suppressed.RuleId, out var metricIdentifier))
      {
        suppressed.Metric = metricIdentifier.ToString();
      }
    }
  }
}

