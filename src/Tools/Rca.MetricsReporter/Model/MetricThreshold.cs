namespace Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Описывает пороговые значения для конкретной метрики.
/// </summary>
public sealed class MetricThreshold
{
    /// <summary>
    /// Граница предупреждения. Для метрик «чем больше тем лучше» — минимально допустимое значение.
    /// </summary>
    public decimal? Warning { get; init; }
        = null;

    /// <summary>
    /// Критическая граница. Для метрик «чем больше тем лучше» — минимально допустимое значение.
    /// </summary>
    public decimal? Error { get; init; }
        = null;

    /// <summary>
    /// Определяет, лучше ли более высокое значение (<see langword="true"/>) либо наоборот.
    /// </summary>
    public bool HigherIsBetter { get; init; }
        = true;
}

