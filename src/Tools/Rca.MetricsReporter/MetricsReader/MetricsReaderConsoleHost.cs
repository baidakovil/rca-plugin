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

      config.AddCommand<MostProblematicCommand>("most-problematic")
        .WithDescription("Returns the most severe symbol that violates the specified metric.")
        .WithExample(new[] { "metrics-reader", "most-problematic", "--namespace", "Rca.Loader", "--metric", "Complexity" });

      config.AddCommand<ListWarningsCommand>("list")
        .WithDescription("Lists all symbols that exceed the specified metric thresholds.")
        .WithExample(new[] { "metrics-reader", "list", "--namespace", "Rca.Loader", "--metric", "Complexity", "--symbol-kind", "Member" });

      config.AddCommand<TestMetricCommand>("test")
        .WithDescription("Checks whether a symbol satisfies the specified metric after refactoring.")
        .WithExample(new[] { "metrics-reader", "test", "--symbol", "Rca.Loader.SomeType.SomeMethod(...)", "--metric", "Complexity" });
    });
  }
}

