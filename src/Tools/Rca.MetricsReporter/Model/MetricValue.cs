namespace Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Represents a single metric value, its delta compared to baseline, and the threshold status.
/// </summary>
public sealed class MetricValue
{
    /// <summary>
    /// Actual metric value. Use <see langword="null"/> when the value is not available.
    /// </summary>
    public decimal? Value { get; init; }

    /// <summary>
    /// Difference from baseline. Use <see langword="null"/> for new members or when no baseline exists.
    /// </summary>
    public decimal? Delta { get; init; }

    /// <summary>
    /// Threshold status for the value.
    /// </summary>
    public ThresholdStatus Status { get; init; } = ThresholdStatus.NotApplicable;

    /// <summary>
    /// Optional unit (for example <c>percent</c>, <c>count</c>, <c>score</c>).
    /// </summary>
    public string? Unit { get; init; }
}

