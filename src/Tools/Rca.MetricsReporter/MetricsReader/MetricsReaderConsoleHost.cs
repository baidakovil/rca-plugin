namespace Rca.Tools.MetricsReporter.MetricsReader;

using System;
using System.Threading;
using System.Threading.Tasks;
using Rca.Tools.MetricsReporter.MetricsReader.Commands;
using Rca.Tools.MetricsReporter.MetricsReader.Settings;
using Spectre.Console.Cli;

/// <summary>
/// Hosts the Spectre.Console CLI that powers metrics-reader commands.
/// </summary>
internal static class MetricsReaderConsoleHost
{
  public static async Task<int> ExecuteAsync(string[] args)
  {
    using var cts = new CancellationTokenSource();
    ConsoleCancelEventHandler handler = (_, eventArgs) =>
    {
      eventArgs.Cancel = true;
      cts.Cancel();
    };

    Console.CancelKeyPress += handler;
    try
    {
      MetricsReaderCancellation.Initialize(cts.Token);
      var app = new CommandApp();
      Configure(app);
      return await app.RunAsync(args).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
      Console.Error.WriteLine("metrics-reader execution cancelled.");
      return 1;
    }
    finally
    {
      Console.CancelKeyPress -= handler;
    }
  }

  private static void Configure(CommandApp app)
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

