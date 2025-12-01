namespace Rca.Tools.MetricsReporter.MetricsReader;

using Rca.Tools.MetricsReporter.MetricsReader.Commands;
using Spectre.Console.Cli;

/// <summary>
/// Configures commands for the metrics-reader CLI application.
/// </summary>
internal static class MetricsReaderCommandConfigurator
{
  /// <summary>
  /// Configures the command application with all available metrics-reader commands.
  /// </summary>
  /// <param name="app">The command application to configure.</param>
  [System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Microsoft.Maintainability",
    "CA1506:AvoidExcessiveClassCoupling",
    Justification = "Command configuration method registers all command types with their descriptions and examples; dependencies on command types are necessary for CLI setup. Further decomposition would fragment the configuration logic without benefit.")]
  public static void Configure(CommandApp app)
  {
    app.Configure(config =>
    {
      config.SetApplicationName("metrics-reader");
      config.ValidateExamples();

      config.AddCommand<ReadAnyCommand>("readany")
        .WithDescription("Reads metric violations for a namespace. Returns the most severe violation by default. Pass --all to list all matches.")
        .WithExample("readany", "--namespace", "Rca.Loader", "--metric", "Complexity")
        .WithExample("readany", "--namespace", "Rca.Loader", "--metric", "Complexity", "--all", "--symbol-kind", "Member");

      config.AddCommand<ReadSarifCommand>("readsarif")
        .WithDescription("Aggregates SARIF-based metrics (SarifCaRuleViolations, SarifIdeRuleViolations) by rule ID for the specified namespace. --metric defaults to Any.")
        .WithExample("readsarif", "--namespace", "Rca.Loader")
        .WithExample("readsarif", "--namespace", "Rca.Loader", "--metric", "SarifIdeRuleViolations", "--all", "--symbol-kind", "Member")
        .WithExample("readsarif", "--namespace", "Rca.Loader", "--metric", "Any", "--ruleid", "CA1506");

      config.AddCommand<TestMetricCommand>("test")
        .WithDescription("Checks whether a symbol satisfies the specified metric after refactoring.")
        .WithExample("test", "--symbol", "Rca.Loader.SomeType.SomeMethod(...)", "--metric", "Complexity");
    });
  }
}

