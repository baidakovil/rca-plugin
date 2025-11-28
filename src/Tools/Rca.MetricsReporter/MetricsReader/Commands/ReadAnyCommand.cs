namespace Rca.Tools.MetricsReporter.MetricsReader.Commands;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rca.Tools.MetricsReporter.MetricsReader.Output;
using Rca.Tools.MetricsReporter.MetricsReader.Services;
using Rca.Tools.MetricsReporter.MetricsReader.Settings;
using Rca.Tools.MetricsReporter.Model;
using Spectre.Console.Cli;

/// <summary>
/// Implements the metrics-reader readany command that unifies the former list and most-problematic flows.
/// </summary>
internal sealed class ReadAnyCommand : MetricsReaderCommandBase<NamespaceMetricSettings>
{
  /// <inheritdoc />
  protected override async Task<int> ExecuteAsync(CommandContext context, NamespaceMetricSettings settings, CancellationToken cancellationToken)
  {
    var trimmedNamespace = settings.Namespace.Trim();
    var engine = await CreateEngineAsync(settings, cancellationToken).ConfigureAwait(false);
    var filter = new SymbolFilter(trimmedNamespace, settings.ResolvedMetric, settings.SymbolKind, settings.IncludeSuppressed);
    var ordered = engine.GetProblematicSymbols(filter)
      .OrderByDescending(snapshot => snapshot.Status == ThresholdStatus.Error ? 2 : 1)
      .ThenByDescending(snapshot => snapshot.Magnitude ?? 0m)
      .ThenBy(snapshot => snapshot.Symbol, StringComparer.Ordinal)
      .Select(SymbolMetricDto.FromSnapshot)
      .ToList();

    if (ordered.Count == 0)
    {
      JsonConsoleWriter.Write(new
      {
        metric = settings.Metric,
        @namespace = trimmedNamespace,
        symbolKind = settings.SymbolKind.ToString(),
        message = $"No violations were found for metric '{settings.Metric}' in namespace '{trimmedNamespace}'."
      });
      return 0;
    }

    if (settings.ShowAll)
    {
      JsonConsoleWriter.Write(ordered);
      return 0;
    }

    JsonConsoleWriter.Write(ordered.FirstOrDefault());
    return 0;
  }
}


