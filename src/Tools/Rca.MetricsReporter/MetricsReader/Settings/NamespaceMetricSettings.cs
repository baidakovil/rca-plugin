namespace Rca.Tools.MetricsReporter.MetricsReader.Settings;

using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Rca.Tools.MetricsReporter.MetricsReader.Services;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Settings shared by commands that operate on namespace + metric scope.
/// </summary>
internal sealed class NamespaceMetricSettings : MetricsReaderSettingsBase
{
  /// <summary>
  /// Gets or sets the namespace filter supplied by the user.
  /// </summary>
  [CommandOption("--namespace <NAME>")]
  [Description("Namespace prefix to filter symbols (e.g. Rca.Loader.Infrastructure).")]
  public string Namespace { get; init; } = string.Empty;

  /// <summary>
  /// Gets or sets the metric identifier or alias provided by the user.
  /// </summary>
  [CommandOption("--metric <NAME>")]
  [Description("Metric identifier or alias (Complexity, Coupling, Maintainability, etc.).")]
  public string Metric { get; init; } = string.Empty;

  [CommandOption("--symbol-kind <Type|Member>")]
  [Description("Symbol level to inspect. Defaults to Type.")]
  public MetricsReaderSymbolKind SymbolKind { get; init; } = MetricsReaderSymbolKind.Type;

  /// <summary>
  /// Gets a value indicating whether all matching entries should be emitted instead of the single most severe one.
  /// </summary>
  [CommandOption("--all")]
  [Description("When specified, emits all matching entries instead of only the most severe one.")]
  public bool ShowAll { get; init; }

  /// <summary>
  /// Gets or sets an optional SARIF rule filter (e.g. CA1506) used by readsarif.
  /// </summary>
  [CommandOption("--ruleid <ID>")]
  [Description("Optional SARIF rule identifier filter (e.g. CA1506, IDE0051).")]
  public string? RuleId { get; init; }

  /// <summary>
  /// Gets the resolved metric identifier after validation succeeds.
  /// </summary>
  public MetricIdentifier ResolvedMetric { get; private set; }

  /// <inheritdoc />
  public override ValidationResult Validate()
  {
    var baseResult = base.Validate();
    if (!baseResult.Successful)
    {
      return baseResult;
    }

    if (string.IsNullOrWhiteSpace(Namespace))
    {
      return ValidationResult.Error("--namespace is required.");
    }

    if (string.IsNullOrWhiteSpace(Metric))
    {
      return ValidationResult.Error("--metric is required.");
    }

    if (!MetricIdentifierResolver.TryResolve(Metric, out var resolved))
    {
      return ValidationResult.Error($"Unknown metric identifier '{Metric}'.");
    }

    ResolvedMetric = resolved;
    return ValidationResult.Success();
  }
}

