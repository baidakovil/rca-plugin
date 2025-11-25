namespace Rca.Tools.MetricsReporter.MetricsReader.Services;

using System;
using System.Collections.Generic;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Provides efficient lookup for suppressed symbols by fully qualified name and metric.
/// </summary>
internal sealed class SuppressedSymbolIndex
{
  private readonly Dictionary<(string Symbol, MetricIdentifier Metric), SuppressedSymbolInfo> _lookup;

  private SuppressedSymbolIndex(Dictionary<(string Symbol, MetricIdentifier Metric), SuppressedSymbolInfo> lookup)
    => _lookup = lookup;

  public static SuppressedSymbolIndex Create(IEnumerable<SuppressedSymbolInfo> entries)
  {
    var lookup = new Dictionary<(string Symbol, MetricIdentifier Metric), SuppressedSymbolInfo>();
    foreach (var entry in entries)
    {
      if (string.IsNullOrWhiteSpace(entry.FullyQualifiedName))
      {
        continue;
      }

      if (!Enum.TryParse(entry.Metric, ignoreCase: true, out MetricIdentifier metric))
      {
        continue;
      }

      lookup[(entry.FullyQualifiedName, metric)] = entry;
    }

    return new SuppressedSymbolIndex(lookup);
  }

  public bool IsSuppressed(string? fullyQualifiedName, MetricIdentifier metric)
  {
    if (string.IsNullOrWhiteSpace(fullyQualifiedName))
    {
      return false;
    }

    return _lookup.ContainsKey((fullyQualifiedName, metric));
  }
}

