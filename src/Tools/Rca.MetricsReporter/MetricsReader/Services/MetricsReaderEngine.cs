namespace Rca.Tools.MetricsReporter.MetricsReader.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using Rca.Tools.MetricsReporter.MetricsReader.Settings;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Provides high-level queries over MetricsReport.g.json for CLI commands.
/// </summary>
internal sealed class MetricsReaderEngine
{
  private readonly MetricsReaderContext _context;

  public MetricsReaderEngine(MetricsReaderContext context)
    => _context = context ?? throw new ArgumentNullException(nameof(context));

  public IEnumerable<SymbolMetricSnapshot> GetProblematicSymbols(SymbolFilter filter)
    => EnumerateSymbols(filter)
      .Where(snapshot => snapshot.Status == ThresholdStatus.Warning || snapshot.Status == ThresholdStatus.Error)
      .Where(snapshot => filter.IncludeSuppressed || !snapshot.IsSuppressed);

  public SymbolMetricSnapshot? TryGetSymbol(string fullyQualifiedName, MetricIdentifier metric)
  {
    foreach (var type in EnumerateTypeNodes())
    {
      if (string.Equals(type.FullyQualifiedName, fullyQualifiedName, StringComparison.Ordinal))
      {
        return BuildSnapshot(type, metric);
      }

      foreach (var member in type.Members)
      {
        if (string.Equals(member.FullyQualifiedName, fullyQualifiedName, StringComparison.Ordinal))
        {
          return BuildSnapshot(member, metric);
        }
      }
    }

    return null;
  }

  private IEnumerable<SymbolMetricSnapshot> EnumerateSymbols(SymbolFilter filter)
  {
    return filter.SymbolKind switch
    {
      MetricsReaderSymbolKind.Type => EnumerateTypeNodes()
        .Where(type => NamespaceMatches(type.FullyQualifiedName, filter.Namespace))
        .Select(node => BuildSnapshot(node, filter.Metric))
        .Where(snapshot => snapshot is not null)
        .Select(snapshot => snapshot!),
      MetricsReaderSymbolKind.Member => EnumerateMemberNodes()
        .Where(member => NamespaceMatches(member.FullyQualifiedName, filter.Namespace))
        .Select(node => BuildSnapshot(node, filter.Metric))
        .Where(snapshot => snapshot is not null)
        .Select(snapshot => snapshot!),
      _ => Enumerable.Empty<SymbolMetricSnapshot>()
    };
  }

  private IEnumerable<TypeMetricsNode> EnumerateTypeNodes()
  {
    foreach (var assembly in _context.Report.Solution.Assemblies)
    {
      foreach (var ns in assembly.Namespaces)
      {
        foreach (var type in ns.Types)
        {
          yield return type;
        }
      }
    }
  }

  private IEnumerable<MemberMetricsNode> EnumerateMemberNodes()
  {
    foreach (var type in EnumerateTypeNodes())
    {
      foreach (var member in type.Members)
      {
        yield return member;
      }
    }
  }

  private SymbolMetricSnapshot? BuildSnapshot(MetricsNode node, MetricIdentifier metric)
  {
    if (!node.Metrics.TryGetValue(metric, out var metricValue) || metricValue is null || metricValue.Value is null)
    {
      return null;
    }

    var level = MapLevel(node.Kind);
    if (level is null)
    {
      return null;
    }

    var threshold = _context.ThresholdProvider.GetThreshold(metric, level.Value);
    var isSuppressed = _context.SuppressedSymbolIndex.IsSuppressed(node.FullyQualifiedName, metric);
    return new SymbolMetricSnapshot(
      node.FullyQualifiedName ?? string.Empty,
      node.Kind,
      node.Source?.Path,
      metric,
      metricValue,
      threshold,
      isSuppressed);
  }

  private static MetricSymbolLevel? MapLevel(CodeElementKind kind)
    => kind switch
    {
      CodeElementKind.Type => MetricSymbolLevel.Type,
      CodeElementKind.Member => MetricSymbolLevel.Member,
      _ => null
    };

  private static bool NamespaceMatches(string? fullyQualifiedName, string namespaceFilter)
  {
    if (string.IsNullOrWhiteSpace(namespaceFilter))
    {
      return true;
    }

    if (string.IsNullOrWhiteSpace(fullyQualifiedName))
    {
      return false;
    }

    if (!fullyQualifiedName.StartsWith(namespaceFilter, StringComparison.Ordinal))
    {
      return false;
    }

    if (fullyQualifiedName.Length == namespaceFilter.Length)
    {
      return true;
    }

    var separator = fullyQualifiedName[namespaceFilter.Length];
    return separator == '.' || separator == '+' || separator == ':';
  }
}

internal sealed record SymbolMetricSnapshot(
  string Symbol,
  CodeElementKind Kind,
  string? FilePath,
  MetricIdentifier Metric,
  MetricValue MetricValue,
  MetricThreshold? Threshold,
  bool IsSuppressed)
{
  public ThresholdStatus Status => MetricValue.Status;

  public decimal? Value => MetricValue.Value;

  public decimal? Delta => MetricValue.Delta;

  public string SymbolType => Kind.ToString();

  public string ThresholdKind => Status switch
  {
    ThresholdStatus.Error => "Error",
    ThresholdStatus.Warning => "Warning",
    _ => "None"
  };

  public decimal? ThresholdValue => Status switch
  {
    ThresholdStatus.Error => Threshold?.Error,
    ThresholdStatus.Warning => Threshold?.Warning,
    _ => null
  };

  public decimal? Magnitude
  {
    get
    {
      if (ThresholdValue is null || Value is null || Threshold is null)
      {
        return null;
      }

      var delta = Threshold.HigherIsBetter
        ? ThresholdValue.Value - Value.Value
        : Value.Value - ThresholdValue.Value;

      return Math.Abs(delta);
    }
  }
}

