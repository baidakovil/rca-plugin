namespace Rca.Tools.MetricsReporter.Model;

using System;
using System.Collections.Generic;

/// <summary>
/// Метаданные, сопровождающие отчёт по метрикам.
/// </summary>
public sealed class ReportMetadata
{
    /// <summary>
    /// Время генерации отчёта в UTC.
    /// </summary>
    public DateTime GeneratedAtUtc { get; init; }
        = DateTime.UtcNow;

    /// <summary>
    /// Описание источника baseline (например, git commit).
    /// </summary>
    public string? BaselineReference { get; init; }
        = null;

    /// <summary>
    /// Пути к основным артефактам.
    /// </summary>
    public ReportPaths Paths { get; init; } = new();

    /// <summary>
    /// Набор пороговых значений по метрикам.
    /// </summary>
    public IDictionary<MetricIdentifier, MetricThreshold> Thresholds { get; init; }
        = new Dictionary<MetricIdentifier, MetricThreshold>();
}

