namespace Rca.Tools.MetricsReporter.MetricsReader.Services;

using System.Collections.Generic;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Builder for creating SARIF violation groups from aggregated violations.
/// </summary>
internal sealed class SarifViolationGroupBuilder
{
  /// <summary>
  /// Initializes a new instance of the <see cref="SarifViolationGroupBuilder"/> class.
  /// </summary>
  /// <param name="ruleId">The rule ID for this group.</param>
  /// <param name="shortDescription">Optional short description of the rule.</param>
  public SarifViolationGroupBuilder(string ruleId, string? shortDescription)
  {
    RuleId = ruleId;
    ShortDescription = shortDescription;
  }

  /// <summary>
  /// Gets the rule ID for this group.
  /// </summary>
  public string RuleId { get; }

  /// <summary>
  /// Gets the optional short description of the rule.
  /// </summary>
  public string? ShortDescription { get; }

  /// <summary>
  /// Gets or sets the total count of violations in this group.
  /// </summary>
  public int Count { get; private set; }

  /// <summary>
  /// Gets the list of violation records in this group.
  /// </summary>
  public List<SarifViolationRecord> Violations { get; } = [];

  /// <summary>
  /// Builds a SARIF violation group from the accumulated data.
  /// </summary>
  /// <returns>A SARIF violation group.</returns>
  public SarifViolationGroup Build()
    => new(RuleId, ShortDescription, Count, Violations);

  /// <summary>
  /// Adds violations from a breakdown entry to this group.
  /// </summary>
  /// <param name="count">The count of violations to add.</param>
  /// <param name="violations">The list of violation details to add.</param>
  /// <param name="node">The metrics node these violations belong to.</param>
  public void Add(int count, IReadOnlyList<SarifRuleViolationDetail> violations, MetricsNode node)
  {
    if (count > 0)
    {
      Count += count;
    }

    if (violations is null || violations.Count == 0)
    {
      return;
    }

    var symbol = node.FullyQualifiedName ?? node.Name ?? string.Empty;
    foreach (var violation in violations)
    {
      Violations.Add(new SarifViolationRecord(
        symbol,
        violation.Message,
        violation.Uri,
        violation.StartLine,
        violation.EndLine));
    }
  }
}

