namespace Rca.Tools.MetricsReporter.MetricsReader.Commands;

using System;
using System.Collections.Generic;
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
internal sealed class ReadSarifCommand : MetricsReaderCommandBase<SarifMetricSettings>
{
  /// <inheritdoc />
  protected override async Task<int> ExecuteAsync(CommandContext context, SarifMetricSettings settings, CancellationToken cancellationToken)
  {
    if (!settings.TryResolveSarifMetrics(out var metrics))
    {
      JsonConsoleWriter.Write(new
      {
        metric = settings.EffectiveMetricName,
        message = $"Metric '{settings.EffectiveMetricName}' does not expose SARIF rule breakdown data. Use SarifCaRuleViolations or SarifIdeRuleViolations."
      });
      return 0;
    }

    var trimmedNamespace = settings.Namespace.Trim();
    var engine = await CreateEngineAsync(settings, cancellationToken).ConfigureAwait(false);
    var aggregatedGroups = new List<SarifViolationGroup>();
    foreach (var metric in metrics)
    {
      var filter = new SymbolFilter(trimmedNamespace, metric, settings.SymbolKind, settings.IncludeSuppressed);
      var aggregation = engine.GetSarifViolationGroups(filter);
      aggregatedGroups.AddRange(aggregation.Groups);
    }

    var sortedGroups = aggregatedGroups
      .OrderByDescending(group => group.Count)
      .ThenBy(group => group.RuleId, StringComparer.OrdinalIgnoreCase)
      .ToList();

    var groups = FilterSarifGroups(sortedGroups, settings);

    if (groups.Count == 0)
    {
      JsonConsoleWriter.Write(new
      {
        metric = settings.EffectiveMetricName,
        @namespace = trimmedNamespace,
        symbolKind = settings.SymbolKind.ToString(),
        ruleId = settings.RuleId,
        message = BuildSarifNotFoundMessage(settings.EffectiveMetricName, trimmedNamespace, settings.RuleId)
      });
      return 0;
    }

    var payload = SarifViolationsResponseDto.From(settings, groups);
    JsonConsoleWriter.Write(payload);
    return 0;
  }
  private static List<SarifViolationGroup> FilterSarifGroups(
    List<SarifViolationGroup> groups,
    SarifMetricSettings settings)
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

  private static string BuildSarifNotFoundMessage(string metric, string @namespace, string? ruleId)
  {
    if (string.IsNullOrWhiteSpace(ruleId))
    {
      return $"No SARIF violations for metric '{metric}' were found within namespace '{@namespace}'.";
    }

    return $"No SARIF violations for metric '{metric}' and rule '{ruleId}' were found within namespace '{@namespace}'.";
  }
}


