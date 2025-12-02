namespace Rca.Tools.MetricsReporter.MetricsReader.Services;

using System;
using System.Collections.Generic;
using Rca.Tools.MetricsReporter.MetricsReader.Output;
using Rca.Tools.MetricsReporter.MetricsReader.Settings;

/// <summary>
/// Handles result formatting and output for ReadSarif command.
/// </summary>
internal sealed class ReadSarifCommandResultHandler : IReadSarifCommandResultHandler
{
  /// <inheritdoc/>
  public void WriteInvalidMetricError(string metricName)
  {
    ArgumentNullException.ThrowIfNull(metricName);

    var dto = new SarifInvalidMetricDto(
      metricName,
      $"Metric '{metricName}' does not expose SARIF rule breakdown data. Use SarifCaRuleViolations or SarifIdeRuleViolations.");
    JsonConsoleWriter.Write(dto);
  }

  /// <inheritdoc/>
  public void WriteNoViolationsFound(string metricName, string @namespace, string symbolKind, string? ruleId)
  {
    ArgumentNullException.ThrowIfNull(metricName);
    ArgumentNullException.ThrowIfNull(@namespace);
    ArgumentNullException.ThrowIfNull(symbolKind);

    var message = BuildNoViolationsMessage(metricName, @namespace, ruleId);
    var dto = new SarifNoViolationsFoundDto(metricName, @namespace, symbolKind, ruleId, message);
    JsonConsoleWriter.Write(dto);
  }

  /// <inheritdoc/>
  public void WriteResponse(SarifMetricSettings settings, IEnumerable<SarifViolationGroup> groups)
  {
    ArgumentNullException.ThrowIfNull(settings);
    ArgumentNullException.ThrowIfNull(groups);

    var payload = SarifViolationsResponseDto.From(settings, groups);
    JsonConsoleWriter.Write(payload);
  }

  private static string BuildNoViolationsMessage(string metric, string @namespace, string? ruleId)
  {
    if (string.IsNullOrWhiteSpace(ruleId))
    {
      return $"No SARIF violations for metric '{metric}' were found within namespace '{@namespace}'.";
    }

    return $"No SARIF violations for metric '{metric}' and rule '{ruleId}' were found within namespace '{@namespace}'.";
  }
}


