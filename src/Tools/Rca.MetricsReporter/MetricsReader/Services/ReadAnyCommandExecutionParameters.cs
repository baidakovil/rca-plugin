namespace Rca.Tools.MetricsReporter.MetricsReader.Services;

using Rca.Tools.MetricsReporter.MetricsReader.Settings;

/// <summary>
/// Parameters for executing the ReadAny command.
/// </summary>
internal sealed record ReadAnyCommandExecutionParameters(
  string Namespace,
  string Metric,
  MetricsReaderSymbolKind SymbolKind,
  bool IncludeSuppressed,
  bool ShowAll);

