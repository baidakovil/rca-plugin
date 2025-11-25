namespace Rca.Tools.MetricsReporter.MetricsReader.Services;

using Rca.Tools.MetricsReporter.MetricsReader.Settings;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Describes the filtering criteria for symbol-level metrics queries.
/// </summary>
internal sealed record SymbolFilter(
  string Namespace,
  MetricIdentifier Metric,
  MetricsReaderSymbolKind SymbolKind,
  bool IncludeSuppressed);

