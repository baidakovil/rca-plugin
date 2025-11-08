using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rca.Tools.MetricsReporter;
using Rca.Tools.MetricsReporter.Services;

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

try
{
    if (args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
    {
        PrintUsage();
        return (int)MetricsReporterExitCode.Success;
    }

    var options = ParseArguments(args);
    var application = new MetricsReporterApplication();
    var exitCode = await application.RunAsync(options, cts.Token).ConfigureAwait(false);
    return (int)exitCode;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Operation was cancelled.");
    return (int)MetricsReporterExitCode.ValidationError;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return (int)MetricsReporterExitCode.ValidationError;
}

static MetricsReporterOptions ParseArguments(string[] args)
{
    var roslynPaths = new List<string>();
    var sarifPaths = new List<string>();

    string? solutionName = null;
    string? metricsDir = null;
    string? altCoverPath = null;
    string? baselinePath = null;
    string? baselineRef = null;
    string? outputJson = null;
    string? outputHtml = null;
    string? thresholds = null;

    for (var index = 0; index < args.Length; index++)
    {
        var argument = args[index];
        switch (argument)
        {
            case "--solution-name":
                solutionName = RequireValue(args, ref index, argument);
                break;
            case "--metrics-dir":
                metricsDir = RequireValue(args, ref index, argument);
                break;
            case "--altcover":
                altCoverPath = RequireValue(args, ref index, argument);
                break;
            case "--roslyn":
                roslynPaths.Add(RequireValue(args, ref index, argument));
                break;
            case "--sarif":
                sarifPaths.Add(RequireValue(args, ref index, argument));
                break;
            case "--baseline":
                baselinePath = RequireValue(args, ref index, argument);
                break;
            case "--baseline-ref":
                baselineRef = RequireValue(args, ref index, argument);
                break;
            case "--output-json":
                outputJson = RequireValue(args, ref index, argument);
                break;
            case "--output-html":
                outputHtml = RequireValue(args, ref index, argument);
                break;
            case "--thresholds":
                thresholds = RequireValue(args, ref index, argument);
                break;
            default:
                throw new ArgumentException($"Unknown argument '{argument}'. Use --help to view usage.", argument);
        }
    }

    if (string.IsNullOrWhiteSpace(metricsDir))
    {
        throw new ArgumentException("--metrics-dir is required.");
    }

    if (string.IsNullOrWhiteSpace(outputJson))
    {
        throw new ArgumentException("--output-json is required.");
    }

    var normalizedMetricsDir = Path.GetFullPath(metricsDir);
    var reportDir = Path.Combine(normalizedMetricsDir, "Report");
    var logFilePath = Path.Combine(reportDir, "metrics-reporter.log");

    return new MetricsReporterOptions
    {
        SolutionName = string.IsNullOrWhiteSpace(solutionName) ? "Solution" : solutionName,
        MetricsDirectory = normalizedMetricsDir,
        AltCoverPath = altCoverPath is null ? null : Path.GetFullPath(altCoverPath),
        RoslynPaths = roslynPaths.Select(Path.GetFullPath).ToArray(),
        SarifPaths = sarifPaths.Select(Path.GetFullPath).ToArray(),
        BaselinePath = baselinePath is null ? null : Path.GetFullPath(baselinePath),
        BaselineReference = baselineRef,
        ThresholdsJson = thresholds,
        OutputJsonPath = Path.GetFullPath(outputJson),
        OutputHtmlPath = string.IsNullOrWhiteSpace(outputHtml) ? string.Empty : Path.GetFullPath(outputHtml),
        LogFilePath = logFilePath
    };
}

static string RequireValue(string[] args, ref int index, string argumentName)
{
    if (index + 1 >= args.Length)
    {
        throw new ArgumentException($"Missing value for {argumentName}.");
    }

    index++;
    return args[index];
}

static void PrintUsage()
{
    Console.WriteLine("RCA Metrics Reporter");
    Console.WriteLine();
    Console.WriteLine("Required parameters:");
    Console.WriteLine("  --metrics-dir <path>    Root directory for metrics artifacts (MetricsDir).");
    Console.WriteLine("  --output-json <path>    Path to the resulting metrics-report.json.");
    Console.WriteLine();
    Console.WriteLine("Optional parameters:");
    Console.WriteLine("  --output-html <path>    Path to the resulting metrics-report.html.");
    Console.WriteLine("  --solution-name <name>  Solution name for the report header.");
    Console.WriteLine("  --altcover <path>       Path to AltCover/OpenCover coverage.xml.");
    Console.WriteLine("  --roslyn <path>         Path to Roslyn metrics XML (repeat for multiple files).");
    Console.WriteLine("  --sarif <path>          Path to SARIF file (repeat for multiple files).");
    Console.WriteLine("  --baseline <path>       Path to baseline metrics JSON.");
    Console.WriteLine("  --baseline-ref <text>   Baseline reference label (git commit, build ID, etc.).");
    Console.WriteLine("  --thresholds <json>     JSON string with metric thresholds.");
}

