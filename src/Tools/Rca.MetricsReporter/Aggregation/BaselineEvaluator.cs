namespace Rca.Tools.MetricsReporter.Aggregation;

using System;
using System.Collections.Generic;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Applies baseline and threshold data to a metrics tree, computing deltas and statuses.
/// </summary>
internal sealed class BaselineEvaluator
{
  /// <summary>
  /// Applies the baseline metrics and thresholds to <paramref name="root"/> recursively.
  /// </summary>
  /// <param name="root">The root metrics node (solution, assembly, namespace, etc.).</param>
  /// <param name="baselineRoot">The optional baseline tree to compare against.</param>
  /// <param name="thresholds">Threshold definitions for metrics evaluation.</param>
  public void Apply(
      MetricsNode root,
      MetricsNode? baselineRoot,
      IDictionary<MetricIdentifier, MetricThresholdDefinition> thresholds)
  {
    var baselineLookup = CreateBaselineLookup(baselineRoot);
    ApplyRecursive(root, baselineLookup, thresholds, root.Name);
  }

  private static Dictionary<string, MetricsNode> CreateBaselineLookup(MetricsNode? baselineRoot)
  {
    var result = new Dictionary<string, MetricsNode>(StringComparer.Ordinal);
    if (baselineRoot is null)
    {
      return result;
    }

    TraverseBaseline(baselineRoot, baselineRoot.Name, result);
    return result;
  }

  private static void TraverseBaseline(MetricsNode node, string path, IDictionary<string, MetricsNode> lookup)
  {
    lookup[path] = node;

    foreach (var assembly in (node as SolutionMetricsNode)?.Assemblies ?? Array.Empty<AssemblyMetricsNode>())
    {
      TraverseBaseline(assembly, $"{path}/{assembly.Name}", lookup);
    }

    foreach (var ns in (node as AssemblyMetricsNode)?.Namespaces ?? Array.Empty<NamespaceMetricsNode>())
    {
      TraverseBaseline(ns, $"{path}/{ns.Name}", lookup);
    }

    foreach (var type in (node as NamespaceMetricsNode)?.Types ?? Array.Empty<TypeMetricsNode>())
    {
      TraverseBaseline(type, $"{path}/{type.Name}", lookup);
    }

    foreach (var member in (node as TypeMetricsNode)?.Members ?? Array.Empty<MemberMetricsNode>())
    {
      TraverseBaseline(member, $"{path}/{member.Name}", lookup);
    }
  }

  private void ApplyRecursive(
      MetricsNode node,
      IReadOnlyDictionary<string, MetricsNode> baselineLookup,
      IDictionary<MetricIdentifier, MetricThresholdDefinition> thresholds,
      string path)
  {
    baselineLookup.TryGetValue(path, out var baselineNode);

    if (node is not SolutionMetricsNode)
    {
      node.IsNew = baselineNode is null;
    }

    var symbolLevel = DetermineSymbolLevel(node);
    node.Metrics = ApplyMetricsBaseline(
        node.Metrics,
        baselineNode?.Metrics ?? new Dictionary<MetricIdentifier, MetricValue>(),
        thresholds,
        symbolLevel);

    switch (node)
    {
      case SolutionMetricsNode solution:
        foreach (var assembly in solution.Assemblies)
        {
          ApplyRecursive(assembly, baselineLookup, thresholds, $"{path}/{assembly.Name}");
        }

        break;
      case AssemblyMetricsNode assembly:
        foreach (var ns in assembly.Namespaces)
        {
          ApplyRecursive(ns, baselineLookup, thresholds, $"{path}/{ns.Name}");
        }

        break;
      case NamespaceMetricsNode @namespace:
        foreach (var type in @namespace.Types)
        {
          ApplyRecursive(type, baselineLookup, thresholds, $"{path}/{type.Name}");
        }

        break;
      case TypeMetricsNode type:
        foreach (var member in type.Members)
        {
          ApplyRecursive(member, baselineLookup, thresholds, $"{path}/{member.Name}");
        }

        break;
    }
  }

  private static IDictionary<MetricIdentifier, MetricValue> ApplyMetricsBaseline(
      IDictionary<MetricIdentifier, MetricValue> metrics,
      IDictionary<MetricIdentifier, MetricValue> baselineMetrics,
      IDictionary<MetricIdentifier, MetricThresholdDefinition> thresholds,
      MetricSymbolLevel symbolLevel)
  {
    var result = new Dictionary<MetricIdentifier, MetricValue>();
    foreach (var identifier in Enum.GetValues<MetricIdentifier>())
    {
      metrics.TryGetValue(identifier, out var current);
      baselineMetrics.TryGetValue(identifier, out var baseline);

      var value = current?.Value;
      var delta = value.HasValue && baseline?.Value is decimal baselineValue
          ? value.Value - baselineValue
          : (decimal?)null;
      if (delta.HasValue && delta.Value == 0)
      {
        delta = null;
      }

      var status = EvaluateStatus(identifier, value, thresholds, symbolLevel);

      if (status == ThresholdStatus.NotApplicable)
      {
        continue;
      }

      // WHY: We preserve the breakdown from the current metric when applying baseline,
      // as breakdown information (e.g., SARIF rule violation details) should not be
      // lost during baseline processing. We copy the breakdown dictionary to avoid
      // sharing references across nodes.
      result[identifier] = new MetricValue
      {
        Value = value,
        Delta = delta,
        Status = status,
        Breakdown = SarifBreakdownHelper.Clone(current?.Breakdown)
      };
    }

    return result;
  }

  private static ThresholdStatus EvaluateStatus(
      MetricIdentifier identifier,
      decimal? value,
      IDictionary<MetricIdentifier, MetricThresholdDefinition> thresholds,
      MetricSymbolLevel symbolLevel)
  {
    if (!value.HasValue)
    {
      return ThresholdStatus.NotApplicable;
    }

    if (!thresholds.TryGetValue(identifier, out var definition))
    {
      return ThresholdStatus.Success;
    }

    if (!TryGetThresholdForLevel(definition.Levels, symbolLevel, out var threshold))
    {
      return ThresholdStatus.Success;
    }

    if (!threshold.Warning.HasValue && !threshold.Error.HasValue)
    {
      return ThresholdStatus.Success;
    }

    return threshold.HigherIsBetter
        ? EvaluateHigherIsBetter(value.Value, threshold)
        : EvaluateLowerIsBetter(value.Value, threshold);
  }

  private static bool TryGetThresholdForLevel(
      IDictionary<MetricSymbolLevel, MetricThreshold> levels,
      MetricSymbolLevel requestedLevel,
      out MetricThreshold threshold)
  {
    if (levels.TryGetValue(requestedLevel, out threshold))
    {
      return true;
    }

    return levels.TryGetValue(MetricSymbolLevel.Type, out threshold);
  }

  private static ThresholdStatus EvaluateHigherIsBetter(decimal value, MetricThreshold threshold)
  {
    if (threshold.Error.HasValue && value < threshold.Error)
    {
      return ThresholdStatus.Error;
    }

    if (threshold.Warning.HasValue && value < threshold.Warning)
    {
      return ThresholdStatus.Warning;
    }

    return ThresholdStatus.Success;
  }

  private static ThresholdStatus EvaluateLowerIsBetter(decimal value, MetricThreshold threshold)
  {
    if (threshold.Error.HasValue && value > threshold.Error)
    {
      return ThresholdStatus.Error;
    }

    if (threshold.Warning.HasValue && value > threshold.Warning)
    {
      return ThresholdStatus.Warning;
    }

    return ThresholdStatus.Success;
  }

  private static MetricSymbolLevel DetermineSymbolLevel(MetricsNode node)
      => node switch
      {
        SolutionMetricsNode => MetricSymbolLevel.Solution,
        AssemblyMetricsNode => MetricSymbolLevel.Assembly,
        NamespaceMetricsNode => MetricSymbolLevel.Namespace,
        TypeMetricsNode => MetricSymbolLevel.Type,
        MemberMetricsNode => MetricSymbolLevel.Member,
        _ => MetricSymbolLevel.Member
      };
}

