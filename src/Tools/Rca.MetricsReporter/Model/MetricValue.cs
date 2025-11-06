namespace Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Представляет значение отдельной метрики, включая дельту и статус относительно порогов.
/// </summary>
public sealed class MetricValue
{
    /// <summary>
    /// Фактическое значение метрики. Для отсутствующих данных используется <see langword="null"/>.
    /// </summary>
    public decimal? Value { get; init; }

    /// <summary>
    /// Отклонение от baseline. Для новых членов или отсутствующего baseline — <see langword="null"/>.
    /// </summary>
    public decimal? Delta { get; init; }

    /// <summary>
    /// Статус относительно пороговых ограничений.
    /// </summary>
    public ThresholdStatus Status { get; init; } = ThresholdStatus.NotApplicable;

    /// <summary>
    /// Единицы измерения (`percent`, `count`, `score`).
    /// </summary>
    public string? Unit { get; init; }
}

