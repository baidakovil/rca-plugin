namespace Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Корневой объект сериализации отчёта.
/// </summary>
public sealed class MetricsReport
{
    /// <summary>
    /// Метаданные, описывающие генерацию отчёта.
    /// </summary>
    public ReportMetadata Metadata { get; init; } = new();

    /// <summary>
    /// Корневой узел solution с иерархией метрик.
    /// </summary>
    public SolutionMetricsNode Solution { get; init; } = new();
}

