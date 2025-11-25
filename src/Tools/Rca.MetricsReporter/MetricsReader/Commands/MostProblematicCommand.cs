namespace Rca.Tools.MetricsReporter.MetricsReader.Commands;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console.Cli;
using Rca.Tools.MetricsReporter.MetricsReader.Output;
using Rca.Tools.MetricsReporter.MetricsReader.Services;
using Rca.Tools.MetricsReporter.MetricsReader.Settings;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Implements the metrics-reader most-problematic command.
/// </summary>
internal sealed class MostProblematicCommand : MetricsReaderCommandBase<NamespaceMetricSettings>
{
  protected override async Task<int> ExecuteAsync(CommandContext context, NamespaceMetricSettings settings, CancellationToken cancellationToken)
  {
    var engine = await CreateEngineAsync(settings, cancellationToken).ConfigureAwait(false);
    var filter = new SymbolFilter(settings.Namespace.Trim(), settings.ResolvedMetric, settings.SymbolKind, settings.IncludeSuppressed);
    var candidate = engine.GetProblematicSymbols(filter)
      .OrderByDescending(snapshot => snapshot.Status == ThresholdStatus.Error ? 2 : 1)
      .ThenByDescending(snapshot => snapshot.Magnitude ?? 0m)
      .ThenBy(snapshot => snapshot.Symbol, StringComparer.Ordinal)
      .FirstOrDefault();

    var payload = candidate is null ? null : SymbolMetricDto.FromSnapshot(candidate);
    JsonConsoleWriter.Write(payload);
    return 0;
  }
}

