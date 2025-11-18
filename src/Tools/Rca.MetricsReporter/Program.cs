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
    string? thresholdsFile = null;
    string? inputJson = null;
    string? excludedAssemblyNames = null;
    string? excludedTypeNamePatterns = null;
    string? excludedMemberNamesPatterns = null;
    bool replaceBaseline = false;
    string? baselineStoragePath = null;
    string? coverageHtmlDir = null;

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
            case "--thresholds-file":
                thresholdsFile = RequireValue(args, ref index, argument);
                break;
            case "--input-json":
                inputJson = RequireValue(args, ref index, argument);
                break;
            case "--excluded-members":
                excludedMemberNamesPatterns = RequireValue(args, ref index, argument);
                break;
            case "--excluded-assemblies":
                excludedAssemblyNames = RequireValue(args, ref index, argument);
                break;
            case "--excluded-types":
                excludedTypeNamePatterns = RequireValue(args, ref index, argument);
                break;
            case "--replace-baseline":
                replaceBaseline = true;
                break;
            case "--baseline-storage-path":
                baselineStoragePath = RequireValue(args, ref index, argument);
                break;
            case "--coverage-html-dir":
                coverageHtmlDir = RequireValue(args, ref index, argument);
                break;
            default:
                throw new ArgumentException($"Unknown argument '{argument}'. Use --help to view usage.", argument);
        }
    }

    // When using --input-json, metrics-dir and output-json are optional
    if (string.IsNullOrWhiteSpace(inputJson))
    {
        if (string.IsNullOrWhiteSpace(metricsDir))
        {
            throw new ArgumentException("--metrics-dir is required when not using --input-json.");
        }

        if (string.IsNullOrWhiteSpace(outputJson))
        {
            throw new ArgumentException("--output-json is required when not using --input-json.");
        }
    }
    else if (string.IsNullOrWhiteSpace(outputHtml))
    {
        throw new ArgumentException("--output-html is required when using --input-json.");
    }

    var normalizedMetricsDir = string.IsNullOrWhiteSpace(metricsDir) ? string.Empty : Path.GetFullPath(metricsDir);
    var reportDir = string.IsNullOrWhiteSpace(normalizedMetricsDir) ? Path.GetTempPath() : Path.Combine(normalizedMetricsDir, "Report");
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
        ThresholdsPath = thresholdsFile is null ? null : Path.GetFullPath(thresholdsFile),
        InputJsonPath = inputJson is null ? null : Path.GetFullPath(inputJson),
        OutputJsonPath = string.IsNullOrWhiteSpace(outputJson) ? string.Empty : Path.GetFullPath(outputJson),
        OutputHtmlPath = string.IsNullOrWhiteSpace(outputHtml) ? string.Empty : Path.GetFullPath(outputHtml),
        LogFilePath = logFilePath,
        ExcludedMemberNamesPatterns = excludedMemberNamesPatterns,
        ExcludedAssemblyNames = excludedAssemblyNames,
        ExcludedTypeNamePatterns = excludedTypeNamePatterns,
        ReplaceMetricsBaseline = replaceBaseline,
        MetricsReportStoragePath = baselineStoragePath is null ? null : Path.GetFullPath(baselineStoragePath),
        CoverageHtmlDir = coverageHtmlDir is null ? null : Path.GetFullPath(coverageHtmlDir)
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
    Console.WriteLine("  --thresholds-file <path> Path to JSON file with symbol-level thresholds.");
    Console.WriteLine("  --input-json <path>     Path to existing metrics-report.json (generates HTML only).");
    Console.WriteLine("  --replace-baseline      Automatically replace baseline if new report differs from existing baseline.");
    Console.WriteLine("  --baseline-storage-path <path> Directory where old baseline files are archived with timestamps.");
    Console.WriteLine("  --excluded-members <list> Comma-separated or semicolon-separated list of member name patterns to exclude.");
    Console.WriteLine("  --excluded-assemblies <list> Comma-separated or semicolon-separated list of assembly patterns to exclude.");
    Console.WriteLine("  --excluded-types <list> Comma-separated or semicolon-separated list of type name patterns to exclude.");
}

