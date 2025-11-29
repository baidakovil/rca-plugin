namespace Rca.Tools.MetricsReporter.Rendering;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Initializes renderer components for HTML table generation.
/// </summary>
internal sealed class TableRendererInitializer
{
  /// <summary>
  /// Initializes renderer components and assigns them via out parameters to reduce coupling.
  /// </summary>
  /// <param name="metricOrder">The order of metrics to display in columns.</param>
  /// <param name="metricUnits">Units associated with each metric.</param>
  /// <param name="report">The metrics report.</param>
  /// <param name="coverageHtmlDir">Optional path to HTML coverage reports directory.</param>
  /// <param name="coverageLinkBuilder">The initialized coverage link builder, or <see langword="null"/> if not needed.</param>
  /// <param name="suppressedIndex">The initialized suppressed index, or <see langword="null"/> if none.</param>
  /// <param name="stateCalculator">The initialized state calculator.</param>
  /// <param name="attributeBuilder">The initialized attribute builder.</param>
  /// <param name="metricCellRenderer">The initialized metric cell renderer.</param>
  [SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling", Justification = "This method's purpose is to initialize multiple renderer components that naturally depend on many types. The coupling has been reduced by extracting creation logic into separate helper methods (CreateCoverageLinkBuilder, CreateRowStateCalculator, CreateRowAttributeBuilder, CreateMetricCellRenderer). Further reduction would require splitting the method into multiple smaller methods, which would harm readability and make the initialization flow harder to follow. The current structure is clear, maintainable, and follows the Single Responsibility Principle by centralizing renderer initialization.")]
  public static void InitializeAndAssign(
    MetricIdentifier[] metricOrder,
    IReadOnlyDictionary<MetricIdentifier, string?> metricUnits,
    MetricsReport report,
    string? coverageHtmlDir,
    out CoverageLinkBuilder? coverageLinkBuilder,
    out Dictionary<(string Fqn, MetricIdentifier Metric), SuppressedSymbolInfo>? suppressedIndex,
    out RowStateCalculator stateCalculator,
    out RowAttributeBuilder attributeBuilder,
    out MetricCellRenderer metricCellRenderer)
  {
    coverageLinkBuilder = CreateCoverageLinkBuilder(coverageHtmlDir);
    suppressedIndex = SuppressionIndexBuilder.Build(report);
    var descendantCountIndex = DescendantCountIndexBuilder.Build(report);
    stateCalculator = CreateRowStateCalculator(metricOrder, suppressedIndex);
    attributeBuilder = CreateRowAttributeBuilder(stateCalculator, descendantCountIndex);
    metricCellRenderer = CreateMetricCellRenderer(metricOrder, metricUnits, suppressedIndex);
  }

  private static CoverageLinkBuilder? CreateCoverageLinkBuilder(string? coverageHtmlDir)
    => string.IsNullOrWhiteSpace(coverageHtmlDir) ? null : new CoverageLinkBuilder(coverageHtmlDir);

  private static RowStateCalculator CreateRowStateCalculator(
    MetricIdentifier[] metricOrder,
    Dictionary<(string Fqn, MetricIdentifier Metric), SuppressedSymbolInfo>? suppressedIndex)
    => new RowStateCalculator(metricOrder, suppressedIndex);

  private static RowAttributeBuilder CreateRowAttributeBuilder(
    RowStateCalculator stateCalculator,
    Dictionary<MetricsNode, int> descendantCountIndex)
    => new RowAttributeBuilder(stateCalculator, descendantCountIndex);

  private static MetricCellRenderer CreateMetricCellRenderer(
    MetricIdentifier[] metricOrder,
    IReadOnlyDictionary<MetricIdentifier, string?> metricUnits,
    Dictionary<(string Fqn, MetricIdentifier Metric), SuppressedSymbolInfo>? suppressedIndex)
    => new MetricCellRenderer(metricOrder, metricUnits, suppressedIndex);
}

