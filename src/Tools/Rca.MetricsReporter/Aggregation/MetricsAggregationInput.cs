namespace Rca.Tools.MetricsReporter.Aggregation;

using System.Collections.Generic;
using Rca.Tools.MetricsReporter.Model;
using Rca.Tools.MetricsReporter.Processing;

/// <summary>
/// Данные, необходимые для построения отчёта по метрикам.
/// </summary>
public sealed class MetricsAggregationInput
{
    /// <summary>
    /// Имя solution, отображаемое в отчёте.
    /// </summary>
    public string SolutionName { get; init; } = "UnknownSolution";

    /// <summary>
    /// Документы AltCover/OpenCover.
    /// </summary>
    public IList<ParsedMetricsDocument> AltCoverDocuments { get; init; } = new List<ParsedMetricsDocument>();

    /// <summary>
    /// Документы Roslyn Code Metrics.
    /// </summary>
    public IList<ParsedMetricsDocument> RoslynDocuments { get; init; } = new List<ParsedMetricsDocument>();

    /// <summary>
    /// Документы SARIF.
    /// </summary>
    public IList<ParsedMetricsDocument> SarifDocuments { get; init; } = new List<ParsedMetricsDocument>();

    /// <summary>
    /// Базовый отчёт, использующийся для вычисления дельт. Может быть <see langword="null"/>.
    /// </summary>
    public MetricsReport? Baseline { get; init; }

    /// <summary>
    /// Пороговые значения по метрикам.
    /// </summary>
    public IDictionary<MetricIdentifier, MetricThreshold> Thresholds { get; init; } = new Dictionary<MetricIdentifier, MetricThreshold>();

    /// <summary>
    /// Пути к основным артефактам.
    /// </summary>
    public ReportPaths Paths { get; init; } = new();

    /// <summary>
    /// Необязательное описание baseline (например, git commit).
    /// </summary>
    public string? BaselineReference { get; init; }
}

