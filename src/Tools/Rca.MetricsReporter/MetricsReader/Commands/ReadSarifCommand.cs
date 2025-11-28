namespace Rca.Tools.MetricsReporter.MetricsReader.Commands;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rca.Tools.MetricsReporter.MetricsReader.Output;
using Rca.Tools.MetricsReporter.MetricsReader.Services;
using Rca.Tools.MetricsReporter.MetricsReader.Settings;
using Rca.Tools.MetricsReporter.Model;
using Spectre.Console.Cli;

/// <summary>
/// Aggregates SARIF-based metric breakdowns by rule identifier.
/// </summary>
internal sealed class ReadSarifCommand : MetricsReaderCommandBase<NamespaceMetricSettings>
{
  /// <inheritdoc />
  protected override async Task<int> ExecuteAsync(CommandContext context, NamespaceMetricSettings settings, CancellationToken cancellationToken)
  {
    if (!SupportsSarifBreakdown(settings.ResolvedMetric))
    {
      JsonConsoleWriter.Write(new
      {
        metric = settings.Metric,
        message = $"Metric '{settings.Metric}' does not expose SARIF rule breakdown data. Use SarifCaRuleViolations or SarifIdeRuleViolations."
      });
      return 0;
    }

    var trimmedNamespace = settings.Namespace.Trim();
    var engine = await CreateEngineAsync(settings, cancellationToken).ConfigureAwait(false);
    var filter = new SymbolFilter(trimmedNamespace, settings.ResolvedMetric, settings.SymbolKind, settings.IncludeSuppressed);
    var aggregation = engine.GetSarifViolationGroups(filter);
    var groups = FilterSarifGroups(aggregation.Groups, settings);

    if (groups.Count == 0)
    {
      JsonConsoleWriter.Write(new
      {
        metric = settings.Metric,
        @namespace = trimmedNamespace,
        symbolKind = settings.SymbolKind.ToString(),
        message = $"No SARIF violations for metric '{settings.Metric}' were found within namespace '{trimmedNamespace}'."
      });
      return 0;
    }

    var payload = SarifViolationsResponseDto.From(settings, groups);
    JsonConsoleWriter.Write(payload);
    return 0;
  }

  private static bool SupportsSarifBreakdown(MetricIdentifier identifier)
    => identifier == MetricIdentifier.SarifCaRuleViolations
      || identifier == MetricIdentifier.SarifIdeRuleViolations;

  private static IReadOnlyList<SarifViolationGroup> FilterSarifGroups(
    IReadOnlyList<SarifViolationGroup> groups,
    NamespaceMetricSettings settings)
  {
    IEnumerable<SarifViolationGroup> query = groups;
    if (!string.IsNullOrWhiteSpace(settings.RuleId))
    {
      query = query.Where(group => string.Equals(group.RuleId, settings.RuleId, StringComparison.OrdinalIgnoreCase));
    }

    if (!settings.ShowAll)
    {
      query = query.Take(1);
    }

    return query.ToList();
  }
}


