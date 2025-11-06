namespace Rca.Tools.MetricsReporter.Model;

using System.Text.Json.Serialization;

/// <summary>
/// Описывает состояние метрики относительно порогов качества.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ThresholdStatus
{
    /// <summary>
    /// Порог не применим или отсутствует.
    /// </summary>
    NotApplicable,

    /// <summary>
    /// Фактическое значение укладывается в целевой диапазон.
    /// </summary>
    Success,

    /// <summary>
    /// Порог частично нарушен, требуется внимание разработчика.
    /// </summary>
    Warning,

    /// <summary>
    /// Критическое нарушение порога качества.
    /// </summary>
    Error,
}

