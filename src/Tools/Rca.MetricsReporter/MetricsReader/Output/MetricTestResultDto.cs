namespace Rca.Tools.MetricsReporter.MetricsReader.Output;

/// <summary>
/// Represents the response of the metrics-reader test command.
/// </summary>
internal sealed class MetricTestResultDto
{
  public bool IsOk { get; init; }

  public SymbolMetricDto? Details { get; init; }

  public string? Message { get; init; }
}

