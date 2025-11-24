using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rca.Tools.MetricsReporter.Services;

namespace Rca.Tools.MetricsReporter;

internal sealed class MetricsReporterConsoleHost
{
  private readonly TextWriter _outputWriter;

  public MetricsReporterConsoleHost(TextWriter outputWriter)
  {
    _outputWriter = outputWriter ?? throw new ArgumentNullException(nameof(outputWriter));
  }

  public static async Task<int> ExecuteAsync(string[] args)
  {
    using var cts = new CancellationTokenSource();
    ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
    {
      eventArgs.Cancel = true;
      cts.Cancel();
    };

    Console.CancelKeyPress += cancelHandler;
    try
    {
      var host = new MetricsReporterConsoleHost(Console.Out);
      var exitCode = await host.RunAsync(args, cts.Token).ConfigureAwait(false);
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
    finally
    {
      Console.CancelKeyPress -= cancelHandler;
    }
  }

  public async Task<MetricsReporterExitCode> RunAsync(string[] args, CancellationToken cancellationToken)
  {
    if (args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
    {
      PrintUsage();
      return MetricsReporterExitCode.Success;
    }

    var options = ParseArguments(args);
    var application = new MetricsReporterApplication();
    return await application.RunAsync(options, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>
  /// Parses raw command-line arguments into strongly-typed <see cref="MetricsReporterOptions"/>.
  /// </summary>
  /// <param name="args">The raw <see cref="string"/> array passed to the process.</param>
  /// <returns>Populated <see cref="MetricsReporterOptions"/> instance.</returns>
  /// <remarks>
  /// This method is <see langword="internal"/> to allow tests in <c>Rca.MetricsReporter.Tests</c>
  /// to verify CLI-to-options binding (e.g., <c>--replace-baseline</c> →
  /// <see cref="MetricsReporterOptions.ReplaceMetricsBaseline"/>).
  /// </remarks>
  internal MetricsReporterOptions ParseArguments(string[] args)
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
    bool analyzeSuppressedSymbols = false;
    string? suppressedSymbolsPath = null;
    string? solutionDirectory = null;
    var sourceCodeFolders = new List<string>();
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
        case "--analyze-suppressed-symbols":
          analyzeSuppressedSymbols = true;
          break;
        case "--suppressed-symbols":
          suppressedSymbolsPath = RequireValue(args, ref index, argument);
          break;
        case "--solution-dir":
          solutionDirectory = RequireValue(args, ref index, argument);
          break;
        case "--source-code-folders":
          var foldersValue = RequireValue(args, ref index, argument);
          // Support comma- or semicolon-separated list
          var folders = foldersValue.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim())
            .Where(f => !string.IsNullOrWhiteSpace(f));
          sourceCodeFolders.AddRange(folders);
          break;
        default:
          throw new ArgumentException($"Unknown argument '{argument}'. Use --help to view usage.", argument);
      }
    }

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
    var logFilePath = Path.Combine(reportDir, "MetricsReporter.log");

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
      ,
      AnalyzeSuppressedSymbols = analyzeSuppressedSymbols,
      SuppressedSymbolsPath = suppressedSymbolsPath is null ? null : Path.GetFullPath(suppressedSymbolsPath),
      SolutionDirectory = solutionDirectory is null ? null : Path.GetFullPath(solutionDirectory),
      SourceCodeFolders = sourceCodeFolders.ToArray()
    };
  }

  private static string RequireValue(string[] args, ref int index, string argumentName)
  {
    if (index + 1 >= args.Length)
    {
      throw new ArgumentException($"Missing value for {argumentName}.");
    }

    index++;
    return args[index];
  }

  private void PrintUsage()
  {
    _outputWriter.WriteLine("RCA Metrics Reporter");
    _outputWriter.WriteLine();
    _outputWriter.WriteLine("Required parameters:");
    _outputWriter.WriteLine("  --metrics-dir <path>    Root directory for metrics artifacts (MetricsDir).");
    _outputWriter.WriteLine("  --output-json <path>    Path to the resulting metrics-report.json.");
    _outputWriter.WriteLine();
    _outputWriter.WriteLine("Optional parameters:");
    _outputWriter.WriteLine("  --output-html <path>    Path to the resulting metrics-report.html.");
    _outputWriter.WriteLine("  --solution-name <name>  Solution name for the report header.");
    _outputWriter.WriteLine("  --altcover <path>       Path to AltCover/OpenCover coverage.xml.");
    _outputWriter.WriteLine("  --roslyn <path>         Path to Roslyn metrics XML (repeat for multiple files).");
    _outputWriter.WriteLine("  --sarif <path>          Path to SARIF file (repeat for multiple files).");
    _outputWriter.WriteLine("  --baseline <path>       Path to baseline metrics JSON.");
    _outputWriter.WriteLine("  --baseline-ref <text>   Baseline reference label (git commit, build ID, etc.).");
    _outputWriter.WriteLine("  --thresholds <json>     JSON string with metric thresholds.");
    _outputWriter.WriteLine("  --thresholds-file <path> Path to JSON file with symbol-level thresholds.");
    _outputWriter.WriteLine("  --input-json <path>     Path to existing metrics-report.json (generates HTML only).");
    _outputWriter.WriteLine("  --replace-baseline      Automatically replace baseline if new report differs from existing baseline.");
    _outputWriter.WriteLine("  --baseline-storage-path <path> Directory where old baseline files are archived with timestamps.");
    _outputWriter.WriteLine("  --excluded-members <list> Comma-separated or semicolon-separated list of member name patterns to exclude.");
    _outputWriter.WriteLine("  --excluded-assemblies <list> Comma-separated or semicolon-separated list of assembly patterns to exclude.");
    _outputWriter.WriteLine("  --excluded-types <list> Comma-separated or semicolon-separated list of type name patterns to exclude.");
    _outputWriter.WriteLine("  --analyze-suppressed-symbols  Analyze SuppressMessage attributes and persist suppressed symbol metadata.");
    _outputWriter.WriteLine("  --suppressed-symbols <path>  Path to JSON file where suppressed symbol metadata will be stored.");
    _outputWriter.WriteLine("  --solution-dir <path>        Root directory of the solution source tree for suppressed symbol analysis.");
    _outputWriter.WriteLine("  --source-code-folders <list> Comma- or semicolon-separated list of source code folder paths (relative to solution-dir)");
    _outputWriter.WriteLine("                               that contain assembly projects. Example: \"src,src/Tools,tests\".");
  }
}

