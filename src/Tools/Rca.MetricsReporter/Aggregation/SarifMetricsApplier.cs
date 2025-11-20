namespace Rca.Tools.MetricsReporter.Aggregation;

using System;
using System.Collections.Generic;
using Rca.Tools.MetricsReporter.Model;
using Rca.Tools.MetricsReporter.Processing;

/// <summary>
/// Applies SARIF-derived metrics into the aggregated solution tree using the line index.
/// </summary>
/// <remarks>
/// Moving this logic out of <see cref="MetricsAggregationService.AggregationWorkspace"/>
/// keeps the workspace focused on orchestration and reduces the cyclomatic complexity of the workspace type.
/// </remarks>
internal sealed class SarifMetricsApplier
{
  private readonly LineIndex _lineIndex;
  private readonly AssemblyFilter _assemblyFilter;
  private readonly Action<MetricsNode, MetricIdentifier, MetricValue> _mergeMetric;

  /// <summary>
  /// Initializes a new instance of the <see cref="SarifMetricsApplier"/> class.
  /// </summary>
  public SarifMetricsApplier(
      LineIndex lineIndex,
      AssemblyFilter assemblyFilter,
      Action<MetricsNode, MetricIdentifier, MetricValue> mergeMetric)
  {
    _lineIndex = lineIndex ?? throw new ArgumentNullException(nameof(lineIndex));
    _assemblyFilter = assemblyFilter ?? throw new ArgumentNullException(nameof(assemblyFilter));
    _mergeMetric = mergeMetric ?? throw new ArgumentNullException(nameof(mergeMetric));
  }

  /// <summary>
  /// Applies all SARIF metrics from <paramref name="document"/> into the provided <paramref name="solution"/>.
  /// </summary>
  public void Apply(ParsedMetricsDocument document, SolutionMetricsNode solution)
  {
    if (document is null)
    {
      throw new ArgumentNullException(nameof(document));
    }

    foreach (var metric in SarifMetricExtractor.Extract(document))
    {
      var target = ResolveTarget(solution, metric);
      _mergeMetric(target, metric.Identifier, metric.Value);
    }
  }

  private MetricsNode ResolveTarget(SolutionMetricsNode solution, SarifMetric metric)
  {
    MetricsNode? target = null;

    if (metric.Line.HasValue)
    {
      target = _lineIndex.FindNode(metric.NormalizedPath, metric.Line.Value);
    }

    if (target is null && _lineIndex.TryGetAssembly(metric.NormalizedPath, out var assembly))
    {
      if (_assemblyFilter.ShouldExcludeAssembly(assembly.FullyQualifiedName))
      {
        return solution;
      }

      target = assembly;
    }

    return target ?? solution;
  }

  private sealed class SarifMetricExtractor
  {
    public static IEnumerable<SarifMetric> Extract(ParsedMetricsDocument document)
    {
      foreach (var element in document.Elements)
      {
        if (element.Source?.Path is null || element.Metrics.Count == 0)
        {
          continue;
        }

        var metric = element.Metrics.First();
        if (metric.Value.Value is null)
        {
          continue;
        }

        var line = element.Source.StartLine ?? element.Source.EndLine;
        var normalizedPath = PathNormalizer.Normalize(element.Source.Path);
        yield return new SarifMetric(normalizedPath, line, metric.Key, metric.Value);
      }
    }
  }

  private sealed record SarifMetric(
      string NormalizedPath,
      int? Line,
      MetricIdentifier Identifier,
      MetricValue Value);
}

