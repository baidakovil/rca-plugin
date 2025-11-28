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

  public SarifViolationAggregationResult GetSarifViolationGroups(SymbolFilter filter)
  {
    var groups = new Dictionary<string, SarifViolationGroupBuilder>(StringComparer.OrdinalIgnoreCase);
    var ruleDescriptions = _context.Report.Metadata.RuleDescriptions ?? new Dictionary<string, RuleDescription>();

    foreach (var node in EnumerateNodes(filter))
    {
      if (!node.Metrics.TryGetValue(filter.Metric, out var metricValue) || metricValue is null)
      {
        continue;
      }

      if (metricValue.Breakdown is null)
      {
        continue;
      }

      if (metricValue.Breakdown.Count == 0)
      {
        continue;
      }

      foreach (var pair in metricValue.Breakdown)
      {
        var entry = pair.Value;
        if (entry is null)
        {
          continue;
        }

        if (!filter.IncludeSuppressed
            && _context.SuppressedSymbolIndex.IsSuppressed(node.FullyQualifiedName, filter.Metric, pair.Key))
        {
          continue;
        }

        if (!groups.TryGetValue(pair.Key, out var builder))
        {
          ruleDescriptions.TryGetValue(pair.Key, out var description);
          builder = new SarifViolationGroupBuilder(pair.Key, description?.ShortDescription);
          groups[pair.Key] = builder;
        }

        builder.Add(entry.Count, entry.Violations, node);
      }
    }

    var ordered = groups.Values
      .Select(builder => builder.Build())
      .OrderByDescending(group => group.Count)
      .ThenBy(group => group.RuleId, StringComparer.OrdinalIgnoreCase)
      .ToList();

    return new SarifViolationAggregationResult(ordered);
  }

  private IEnumerable<SymbolMetricSnapshot> EnumerateSymbols(SymbolFilter filter)
  {
    return filter.SymbolKind switch
    {
      MetricsReaderSymbolKind.Type => EnumerateTypeSnapshots(filter),
      MetricsReaderSymbolKind.Member => EnumerateMemberSnapshots(filter),
      MetricsReaderSymbolKind.Any => EnumerateTypeSnapshots(filter).Concat(EnumerateMemberSnapshots(filter)),
      _ => Enumerable.Empty<SymbolMetricSnapshot>()
    };
  }

  private IEnumerable<MetricsNode> EnumerateNodes(SymbolFilter filter)
  {
    return filter.SymbolKind switch
    {
      MetricsReaderSymbolKind.Type => EnumerateTypeNodes()
        .Where(type => NamespaceMatches(type.FullyQualifiedName, filter.Namespace))
        .Cast<MetricsNode>(),
      MetricsReaderSymbolKind.Member => EnumerateMemberNodes()
        .Where(member => NamespaceMatches(member.FullyQualifiedName, filter.Namespace))
        .Cast<MetricsNode>(),
      MetricsReaderSymbolKind.Any => EnumerateTypeNodes()
        .Where(type => NamespaceMatches(type.FullyQualifiedName, filter.Namespace))
        .Cast<MetricsNode>()
        .Concat(EnumerateMemberNodes()
          .Where(member => NamespaceMatches(member.FullyQualifiedName, filter.Namespace))
          .Cast<MetricsNode>()),
      _ => Enumerable.Empty<MetricsNode>()
    };
  }

  private IEnumerable<SymbolMetricSnapshot> EnumerateTypeSnapshots(SymbolFilter filter)
    => EnumerateTypeNodes()
      .Where(type => NamespaceMatches(type.FullyQualifiedName, filter.Namespace))
      .Select(node => BuildSnapshot(node, filter.Metric))
      .Where(snapshot => snapshot is not null)
      .Select(snapshot => snapshot!);

  private IEnumerable<SymbolMetricSnapshot> EnumerateMemberSnapshots(SymbolFilter filter)
    => EnumerateMemberNodes()
      .Where(member => NamespaceMatches(member.FullyQualifiedName, filter.Namespace))
      .Select(node => BuildSnapshot(node, filter.Metric))
      .Where(snapshot => snapshot is not null)
      .Select(snapshot => snapshot!);

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

internal sealed record SarifViolationAggregationResult(
  IReadOnlyList<SarifViolationGroup> Groups);

internal sealed record SarifViolationGroup(
  string RuleId,
  string? ShortDescription,
  int Count,
  IReadOnlyList<SarifViolationRecord> Violations);

internal sealed record SarifViolationRecord(
  string Symbol,
  string? Message,
  string? Uri,
  int? StartLine,
  int? EndLine);

file sealed class SarifViolationGroupBuilder
{
  public SarifViolationGroupBuilder(string ruleId, string? shortDescription)
  {
    RuleId = ruleId;
    ShortDescription = shortDescription;
  }

  public string RuleId { get; }

  public string? ShortDescription { get; }

  public int Count { get; private set; }

  public List<SarifViolationRecord> Violations { get; } = new();

  public SarifViolationGroup Build()
    => new(RuleId, ShortDescription, Count, Violations);

  public void Add(int count, IReadOnlyList<SarifRuleViolationDetail> violations, MetricsNode node)
  {
    if (count > 0)
    {
      Count += count;
    }

    if (violations is null || violations.Count == 0)
    {
      return;
    }

    var symbol = node.FullyQualifiedName ?? node.Name ?? string.Empty;
    foreach (var violation in violations)
    {
      Violations.Add(new SarifViolationRecord(
        symbol,
        violation.Message,
        violation.Uri,
        violation.StartLine,
        violation.EndLine));
    }
  }
}

