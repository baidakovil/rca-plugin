namespace Rca.Tools.MetricsReporter.MetricsReader.Output;

using System.Collections.Generic;
using System.Linq;
using Rca.Tools.MetricsReporter.MetricsReader.Services;
using Rca.Tools.MetricsReporter.MetricsReader.Settings;

/// <summary>
/// JSON payload returned by metrics-reader readsarif.
/// </summary>
internal sealed class SarifViolationsResponseDto
{
  public string Metric { get; init; } = string.Empty;

  public string Namespace { get; init; } = string.Empty;

  public string SymbolKind { get; init; } = string.Empty;

  public bool IncludeSuppressed { get; init; }

  public List<SarifViolationGroupDto> ViolationsGroups { get; init; } = new();

  public static SarifViolationsResponseDto From(SarifMetricSettings settings, IEnumerable<SarifViolationGroup> groups)
    => new()
    {
      Metric = settings.EffectiveMetricName,
      Namespace = settings.Namespace.Trim(),
      SymbolKind = settings.SymbolKind.ToString(),
      IncludeSuppressed = settings.IncludeSuppressed,
      ViolationsGroups = groups
        .Select(SarifViolationGroupDto.FromModel)
        .ToList()
    };
}

internal sealed class SarifViolationGroupDto
{
  public string RuleId { get; init; } = string.Empty;

  public string? ShortDescription { get; init; }

  public int Count { get; init; }

  public List<SarifViolationDetailDto> Violations { get; init; } = new();

  public static SarifViolationGroupDto FromModel(SarifViolationGroup group)
    => new()
    {
      RuleId = group.RuleId,
      ShortDescription = group.ShortDescription,
      Count = group.Count,
      Violations = group.Violations.Select(SarifViolationDetailDto.FromModel).ToList()
    };
}

internal sealed class SarifViolationDetailDto
{
  public string Symbol { get; init; } = string.Empty;

  public string? Message { get; init; }

  public string? Uri { get; init; }

  public int? StartLine { get; init; }

  public int? EndLine { get; init; }

  public static SarifViolationDetailDto FromModel(SarifViolationRecord record)
    => new()
    {
      Symbol = record.Symbol,
      Message = record.Message,
      Uri = record.Uri,
      StartLine = record.StartLine,
      EndLine = record.EndLine
    };
}


