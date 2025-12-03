namespace Rca.Tools.MetricsReporter.MetricsReader.Services;

/// <summary>
/// Parameters for handling ReadAny command results.
/// </summary>
internal sealed record ReadAnyCommandResultParameters(
  string Metric,
  string Namespace,
  string SymbolKind,
  bool ShowAll);





