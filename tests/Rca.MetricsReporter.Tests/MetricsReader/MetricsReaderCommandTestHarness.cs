namespace Rca.MetricsReporter.Tests.MetricsReader;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Rca.Tools.MetricsReporter.MetricsReader;
using Rca.Tools.MetricsReporter.MetricsReader.Settings;
using Spectre.Console.Cli;

/// <summary>
/// Provides helpers for executing metrics-reader commands under test.
/// </summary>
internal static class MetricsReaderCommandTestHarness
{
  public static Task<(int ExitCode, string Output)> RunNamespaceCommandAsync<TCommand>(NamespaceMetricSettings settings)
    where TCommand : class, ICommand
  {
    ArgumentNullException.ThrowIfNull(settings);
    var args = BuildNamespaceArguments(settings);
    return RunCommandAsync<TCommand>(args);
  }

  public static Task<(int ExitCode, string Output)> RunTestCommandAsync<TCommand>(TestMetricSettings settings)
    where TCommand : class, ICommand
  {
    ArgumentNullException.ThrowIfNull(settings);
    var args = BuildTestArguments(settings);
    return RunCommandAsync<TCommand>(args);
  }

  private static async Task<(int ExitCode, string Output)> RunCommandAsync<TCommand>(string[] args)
    where TCommand : class, ICommand
  {
    MetricsReaderCancellation.Initialize(CancellationToken.None);
    var commandApp = new CommandApp<TCommand>();
    var originalOut = Console.Out;
    using var writer = new StringWriter();
    Console.SetOut(writer);
    try
    {
      var exitCode = await commandApp.RunAsync(args).ConfigureAwait(false);
      return (exitCode, writer.ToString());
    }
    finally
    {
      Console.SetOut(originalOut);
    }
  }

  private static string[] BuildNamespaceArguments(NamespaceMetricSettings settings)
  {
    var args = new List<string>
    {
      "--report", settings.ReportPath,
      "--namespace", settings.Namespace,
      "--metric", settings.Metric,
      "--symbol-kind", settings.SymbolKind.ToString()
    };

    if (!string.IsNullOrWhiteSpace(settings.RuleId))
    {
      args.Add("--ruleid");
      args.Add(settings.RuleId!);
    }

    if (settings.ShowAll)
    {
      args.Add("--all");
    }

    AppendCommonArguments(args, settings.IncludeSuppressed, settings.ThresholdsFile, settings.Update);
    return args.ToArray();
  }

  private static string[] BuildTestArguments(TestMetricSettings settings)
  {
    var args = new List<string>
    {
      "--report", settings.ReportPath,
      "--symbol", settings.Symbol,
      "--metric", settings.Metric
    };

    AppendCommonArguments(args, settings.IncludeSuppressed, settings.ThresholdsFile, settings.Update);
    return args.ToArray();
  }

  private static void AppendCommonArguments(
    List<string> args,
    bool includeSuppressed,
    string? thresholdsFile,
    bool update)
  {
    if (includeSuppressed)
    {
      args.Add("--include-suppressed");
    }

    if (!string.IsNullOrWhiteSpace(thresholdsFile))
    {
      args.Add("--thresholds-file");
      args.Add(thresholdsFile!);
    }

    if (update)
    {
      args.Add("--update");
    }
  }
}

