namespace Rca.Tools.MetricsReporter.MetricsReader.Services;

using System.Collections.Generic;
using Rca.Tools.MetricsReporter.MetricsReader.Settings;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Parameters for executing the ReadSarif command.
/// </summary>
internal sealed record ReadSarifCommandExecutionParameters(
  string Namespace,
  IReadOnlyList<MetricIdentifier> Metrics,
  MetricsReaderSymbolKind SymbolKind,
  bool IncludeSuppressed,
  string? RuleId,
  bool ShowAll);

