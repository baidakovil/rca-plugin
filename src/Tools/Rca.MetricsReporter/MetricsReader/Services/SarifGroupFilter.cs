namespace Rca.Tools.MetricsReporter.MetricsReader.Services;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Filters SARIF violation groups based on criteria.
/// </summary>
internal sealed class SarifGroupFilter : ISarifGroupFilter
{
  /// <inheritdoc/>
  public List<SarifViolationGroup> Filter(List<SarifViolationGroup> groups, string? ruleId, bool showAll)
  {
    ArgumentNullException.ThrowIfNull(groups);

    IEnumerable<SarifViolationGroup> query = groups;

    if (!string.IsNullOrWhiteSpace(ruleId))
    {
      query = query.Where(group => string.Equals(group.RuleId, ruleId, StringComparison.OrdinalIgnoreCase));
    }

    if (!showAll)
    {
      query = query.Take(1);
    }

    return query.ToList();
  }
}



