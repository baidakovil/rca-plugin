namespace Rca.Tools.MetricsReporter.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Rca.Tools.MetricsReporter.Aggregation;
using Rca.Tools.MetricsReporter.Configuration;
using Rca.Tools.MetricsReporter.Logging;
using Rca.Tools.MetricsReporter.Model;
using Rca.Tools.MetricsReporter.Processing;
using Rca.Tools.MetricsReporter.Processing.Parsers;
using Rca.Tools.MetricsReporter.Rendering;
using Rca.Tools.MetricsReporter.Serialization;

/// <summary>
/// Coordinates the aggregation workflow and report generation.
/// </summary>
public sealed class MetricsReporterApplication
{
  private readonly AltCoverMetricsParser _altCoverParser = new();
  private readonly RoslynMetricsParser _roslynParser = new();
  private readonly SarifMetricsParser _sarifParser = new();
  private readonly MetricsAggregationService _aggregationService;

  /// <summary>
  /// Initializes a new instance of the <see cref="MetricsReporterApplication"/> class.
  /// </summary>
  public MetricsReporterApplication()
  {
    _aggregationService = new MetricsAggregationService();
  }

  /// <summary>
  /// Initializes a new instance of the <see cref="MetricsReporterApplication"/> class with the specified member filter.
  /// </summary>
  /// <param name="memberFilter">The member filter to use for excluding methods.</param>
  public MetricsReporterApplication(MemberFilter memberFilter)
  {
    _aggregationService = new MetricsAggregationService(memberFilter, new AssemblyFilter(), new TypeFilter());
  }

  /// <summary>
  /// Executes the aggregation process.
  /// </summary>
  /// <param name="options">The options for the metrics reporter.</param>
  /// <param name="cancellationToken">Cancellation token for async operations.</param>
  /// <returns>The exit code indicating success or failure.</returns>
  public async Task<MetricsReporterExitCode> RunAsync(MetricsReporterOptions options, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(options);

    using var logger = new FileLogger(options.LogFilePath);
    logger.LogInformation("Metrics Reporter started.");

    // Log raw command-line arguments to diagnose CLI-to-options binding (especially bool flags).
    try
    {
      var cliArgs = Environment.GetCommandLineArgs();
      logger.LogInformation($"CLI args: {string.Join(" | ", cliArgs)}");
    }
    catch (Exception)
    {
      // Swallow any environment-related exceptions; argument logging is best-effort only.
    }

    // If input JSON is specified, load it and generate HTML only
    if (!string.IsNullOrWhiteSpace(options.InputJsonPath))
    {
      return await GenerateHtmlFromJsonAsync(options, logger, cancellationToken).ConfigureAwait(false);
    }

    var validationResult = ValidateOptionsWithLogging(options, logger);
    if (validationResult != MetricsReporterExitCode.Success)
    {
      return validationResult;
    }

    var thresholdsResult = LoadThresholdsWithLogging(options, logger);
    if (thresholdsResult.ExitCode != MetricsReporterExitCode.Success)
    {
      return thresholdsResult.ExitCode;
    }

    // Capture initial state so we can distinguish the very first run (no previous report/baseline)
    // from subsequent runs when deciding whether to replace the baseline at the end.
    var hadReportAtStart = !string.IsNullOrWhiteSpace(options.OutputJsonPath) && File.Exists(options.OutputJsonPath);
    var hadBaselineAtStart = !string.IsNullOrWhiteSpace(options.BaselinePath) && File.Exists(options.BaselinePath);

    // Baseline management is controlled solely by ReplaceMetricsBaseline, which is driven by
    // the command-line flag --replace-baseline emitted by MSBuild.
    var replaceBaselineEnabled = options.ReplaceMetricsBaseline;

    logger.LogInformation(
      $"Baseline debug: ReplaceMetricsBaseline={options.ReplaceMetricsBaseline}, " +
      $"EffectiveReplaceBaseline={replaceBaselineEnabled}, " +
      $"BaselinePath='{options.BaselinePath ?? "(null)"}', " +
      $"OutputJsonPath='{options.OutputJsonPath}', " +
      $"MetricsReportStoragePath='{options.MetricsReportStoragePath ?? "(null)"}', " +
      $"hadReportAtStart={hadReportAtStart}, hadBaselineAtStart={hadBaselineAtStart}");

    // Create baseline from previous report if baseline doesn't exist and ReplaceMetricsBaseline is enabled.
    // This allows the new report to be generated with deltas calculated against the previous report.
    if (replaceBaselineEnabled && !string.IsNullOrWhiteSpace(options.BaselinePath) && !hadBaselineAtStart)
    {
      if (hadReportAtStart)
      {
        logger.LogInformation("Baseline does not exist. Creating baseline from previous report...");
        await BaselineManager.CreateBaselineFromPreviousReportAsync(
            options.OutputJsonPath,
            options.BaselinePath,
            logger,
            cancellationToken).ConfigureAwait(false);
      }
      else
      {
        logger.LogInformation("Baseline does not exist and previous report not found. New report will be generated without baseline.");
      }
    }

    var baseline = await BaselineLoader.LoadAsync(options.BaselinePath, cancellationToken).ConfigureAwait(false);

    // Optionally compute suppressed symbol metadata before parsing metrics so that
    // both the standalone JSON artefact and the final report share the same view.
    List<SuppressedSymbolInfo> suppressedSymbols = new();
    if (options.AnalyzeSuppressedSymbols)
    {
      try
      {
        if (string.IsNullOrWhiteSpace(options.SuppressedSymbolsPath))
        {
          throw new ArgumentException("Suppressed symbols path must be specified when AnalyzeSuppressedSymbols is enabled.", nameof(options));
        }

        var suppressionRoot = ResolveSuppressedSymbolsRootDirectory(options);
        var sourceCodeFolders = options.SourceCodeFolders ?? Array.Empty<string>();
        logger.LogInformation($"Analyzing suppressed symbols via Roslyn (root: '{suppressionRoot}', source folders: [{string.Join(", ", sourceCodeFolders)}], excluded assemblies: '{options.ExcludedAssemblyNames ?? string.Empty}').");
        var suppressedReport = Processing.SuppressedSymbolsAnalyzer.Analyze(suppressionRoot, sourceCodeFolders, options.ExcludedAssemblyNames, cancellationToken);

        var suppressedDirectory = Path.GetDirectoryName(options.SuppressedSymbolsPath);
        if (!string.IsNullOrWhiteSpace(suppressedDirectory) && !Directory.Exists(suppressedDirectory))
        {
          Directory.CreateDirectory(suppressedDirectory);
        }

        await SuppressedSymbolsWriter.WriteAsync(suppressedReport, options.SuppressedSymbolsPath, cancellationToken).ConfigureAwait(false);
        suppressedSymbols = suppressedReport.SuppressedSymbols.ToList();
        logger.LogInformation($"Suppressed symbols analysis completed. Entries: {suppressedSymbols.Count}");
      }
      catch (Exception ex)
      {
        logger.LogError("Failed to analyze suppressed symbols. Proceeding without suppression metadata.", ex);
        suppressedSymbols = new();
      }
    }
    else
    {
      // If analysis is disabled, still try to load pre-existing suppression metadata
      // so that manual or previous runs can be reused.
      suppressedSymbols = (await SuppressedSymbolsLoader.LoadAsync(options.SuppressedSymbolsPath, cancellationToken).ConfigureAwait(false)).ToList();
    }

    var documentsResult = await ParseAllDocumentsAsync(options, logger, cancellationToken).ConfigureAwait(false);
    if (documentsResult.ExitCode != MetricsReporterExitCode.Success)
    {
      return documentsResult.ExitCode;
    }

    var aggregationInput = BuildAggregationInput(options, documentsResult, thresholdsResult.Thresholds, baseline, suppressedSymbols);
    var report = BuildReportWithLogging(aggregationInput, options, logger);
    if (report is null)
    {
      return MetricsReporterExitCode.ValidationError;
    }

    // Write report to output location
    var writeResult = await WriteReportsAsync(report, options, logger, cancellationToken).ConfigureAwait(false);
    if (writeResult != MetricsReporterExitCode.Success)
    {
      return writeResult;
    }

    // Replace baseline with newly generated report if enabled and there was a previous
    // report or baseline at the start of the run. This ensures that baselines are created
    // only from _previous_ reports and not from the very first run with no history.
    if (replaceBaselineEnabled
        && !string.IsNullOrWhiteSpace(options.BaselinePath)
        && (hadReportAtStart || hadBaselineAtStart))
    {
      await BaselineManager.ReplaceBaselineAsync(
          options.OutputJsonPath,
          options.BaselinePath,
          options.MetricsReportStoragePath,
          logger,
          cancellationToken).ConfigureAwait(false);
    }

    logger.LogInformation("Metrics Reporter completed successfully.");
    return MetricsReporterExitCode.Success;
  }

  private static string ResolveSuppressedSymbolsRootDirectory(MetricsReporterOptions options)
  {
    // 1. Explicit solution directory wins if it looks valid.
    if (!string.IsNullOrWhiteSpace(options.SolutionDirectory))
    {
      var explicitRoot = Path.GetFullPath(options.SolutionDirectory);
      if (Directory.Exists(explicitRoot))
      {
        return explicitRoot;
      }
    }

    // 2. Otherwise start from MetricsDirectory (if available) or the process base directory
    // and walk upwards until we find a directory that contains a solution file (*.sln).
    var startDirectory = !string.IsNullOrWhiteSpace(options.MetricsDirectory)
      ? Path.GetFullPath(options.MetricsDirectory)
      : AppContext.BaseDirectory;

    var currentDirectory = new DirectoryInfo(startDirectory);
    while (currentDirectory is not null)
    {
      try
      {
        var hasSolution = currentDirectory.GetFiles("*.sln").Length > 0;
        if (hasSolution)
        {
          return currentDirectory.FullName;
        }
      }
      catch (IOException)
      {
        // Ignore IO issues and continue walking up the tree.
      }
      catch (UnauthorizedAccessException)
      {
        // Ignore permission issues when probing for solution files.
      }

      currentDirectory = currentDirectory.Parent;
    }

    // 3. Fallback to the original starting directory if no solution file was discovered.
    return startDirectory;
  }

  /// <summary>
  /// Validates options and logs any errors.
  /// </summary>
  /// <param name="options">The options to validate.</param>
  /// <param name="logger">The logger to use for error messages.</param>
  /// <returns>The exit code indicating validation result.</returns>
  private static MetricsReporterExitCode ValidateOptionsWithLogging(MetricsReporterOptions options, FileLogger logger)
  {
    try
    {
      ValidateOptions(options);
      return MetricsReporterExitCode.Success;
    }
    catch (Exception ex)
    {
      logger.LogError(ex.Message, ex);
      return MetricsReporterExitCode.ValidationError;
    }
  }

  /// <summary>
  /// Loads thresholds and logs any errors.
  /// </summary>
  /// <param name="options">The options containing threshold configuration.</param>
  /// <param name="logger">The logger to use for error messages.</param>
  /// <returns>A result containing the exit code and loaded thresholds.</returns>
  private (MetricsReporterExitCode ExitCode, IDictionary<MetricIdentifier, MetricThresholdDefinition> Thresholds) LoadThresholdsWithLogging(
      MetricsReporterOptions options,
      FileLogger logger)
  {
    try
    {
      var thresholds = ParseThresholds(options);
      return (MetricsReporterExitCode.Success, thresholds);
    }
    catch (Exception ex)
    {
      logger.LogError(ex.Message, ex);
      return (MetricsReporterExitCode.ValidationError, new Dictionary<MetricIdentifier, MetricThresholdDefinition>());
    }
  }

  /// <summary>
  /// Parses all input documents (AltCover, Roslyn, SARIF).
  /// </summary>
  /// <param name="options">The options containing document paths.</param>
  /// <param name="logger">The logger to use for progress messages.</param>
  /// <param name="cancellationToken">Cancellation token for async operations.</param>
  /// <returns>A result containing the exit code and parsed documents.</returns>
  private async Task<(MetricsReporterExitCode ExitCode, IList<ParsedMetricsDocument> AltCoverDocuments, IList<ParsedMetricsDocument> RoslynDocuments, IList<ParsedMetricsDocument> SarifDocuments)> ParseAllDocumentsAsync(
      MetricsReporterOptions options,
      FileLogger logger,
      CancellationToken cancellationToken)
  {
    var altCoverDocuments = await ParseAltCoverDocumentsAsync(options, logger, cancellationToken).ConfigureAwait(false);
    if (altCoverDocuments is null)
    {
      return (MetricsReporterExitCode.ParsingError, new List<ParsedMetricsDocument>(), new List<ParsedMetricsDocument>(), new List<ParsedMetricsDocument>());
    }

    var roslynDocuments = await ParseRoslynDocumentsAsync(options, logger, cancellationToken).ConfigureAwait(false);
    if (roslynDocuments is null)
    {
      return (MetricsReporterExitCode.ParsingError, altCoverDocuments, new List<ParsedMetricsDocument>(), new List<ParsedMetricsDocument>());
    }

    var sarifDocuments = await ParseSarifDocumentsAsync(options, logger, cancellationToken).ConfigureAwait(false);
    if (sarifDocuments is null)
    {
      return (MetricsReporterExitCode.ParsingError, altCoverDocuments, roslynDocuments, new List<ParsedMetricsDocument>());
    }

    return (MetricsReporterExitCode.Success, altCoverDocuments, roslynDocuments, sarifDocuments);
  }

  /// <summary>
  /// Parses AltCover documents.
  /// </summary>
  /// <param name="options">The options containing AltCover path.</param>
  /// <param name="logger">The logger to use for progress messages.</param>
  /// <param name="cancellationToken">Cancellation token for async operations.</param>
  /// <returns>A list of parsed documents, or <see langword="null"/> if parsing failed.</returns>
  private async Task<IList<ParsedMetricsDocument>?> ParseAltCoverDocumentsAsync(
      MetricsReporterOptions options,
      FileLogger logger,
      CancellationToken cancellationToken)
  {
    var documents = new List<ParsedMetricsDocument>();
    if (string.IsNullOrWhiteSpace(options.AltCoverPath))
    {
      return documents;
    }

    var document = await ParseSafeAsync(_altCoverParser, options.AltCoverPath, logger, cancellationToken).ConfigureAwait(false);
    if (document is null)
    {
      return null;
    }

    documents.Add(document);
    return documents;
  }

  /// <summary>
  /// Parses Roslyn documents.
  /// </summary>
  /// <param name="options">The options containing Roslyn paths.</param>
  /// <param name="logger">The logger to use for progress messages.</param>
  /// <param name="cancellationToken">Cancellation token for async operations.</param>
  /// <returns>A list of parsed documents, or <see langword="null"/> if parsing failed.</returns>
  private async Task<IList<ParsedMetricsDocument>?> ParseRoslynDocumentsAsync(
      MetricsReporterOptions options,
      FileLogger logger,
      CancellationToken cancellationToken)
  {
    var documents = new List<ParsedMetricsDocument>();
    foreach (var path in options.RoslynPaths)
    {
      var document = await ParseSafeAsync(_roslynParser, path, logger, cancellationToken).ConfigureAwait(false);
      if (document is null)
      {
        return null;
      }

      documents.Add(document);
    }

    return documents;
  }

  /// <summary>
  /// Parses SARIF documents.
  /// </summary>
  /// <param name="options">The options containing SARIF paths.</param>
  /// <param name="logger">The logger to use for progress messages.</param>
  /// <param name="cancellationToken">Cancellation token for async operations.</param>
  /// <returns>A list of parsed documents, or <see langword="null"/> if parsing failed.</returns>
  private async Task<IList<ParsedMetricsDocument>?> ParseSarifDocumentsAsync(
      MetricsReporterOptions options,
      FileLogger logger,
      CancellationToken cancellationToken)
  {
    var documents = new List<ParsedMetricsDocument>();
    foreach (var path in options.SarifPaths)
    {
      var document = await ParseSafeAsync(_sarifParser, path, logger, cancellationToken).ConfigureAwait(false);
      if (document is null)
      {
        return null;
      }

      documents.Add(document);
    }

    return documents;
  }

  /// <summary>
  /// Builds the aggregation input from parsed documents and configuration.
  /// </summary>
  /// <param name="options">The options containing configuration.</param>
  /// <param name="documentsResult">The parsed documents.</param>
  /// <param name="thresholds">The threshold definitions.</param>
  /// <param name="baseline">The baseline report, if any.</param>
  /// <returns>The aggregation input ready for report building.</returns>
  private static MetricsAggregationInput BuildAggregationInput(
      MetricsReporterOptions options,
      (MetricsReporterExitCode ExitCode, IList<ParsedMetricsDocument> AltCoverDocuments, IList<ParsedMetricsDocument> RoslynDocuments, IList<ParsedMetricsDocument> SarifDocuments) documentsResult,
      IDictionary<MetricIdentifier, MetricThresholdDefinition> thresholds,
      MetricsReport? baseline,
      List<SuppressedSymbolInfo> suppressedSymbols)
  {
    var memberFilter = MemberFilter.FromString(options.ExcludedMemberNamesPatterns);
    var assemblyFilter = AssemblyFilter.FromString(options.ExcludedAssemblyNames);
    var typeFilter = TypeFilter.FromString(options.ExcludedTypeNamePatterns);

    return new MetricsAggregationInput
    {
      SolutionName = options.SolutionName,
      AltCoverDocuments = documentsResult.AltCoverDocuments,
      RoslynDocuments = documentsResult.RoslynDocuments,
      SarifDocuments = documentsResult.SarifDocuments,
      Baseline = baseline,
      Thresholds = thresholds,
      Paths = new ReportPaths
      {
        MetricsDirectory = options.MetricsDirectory,
        Baseline = options.BaselinePath,
        Report = options.OutputJsonPath,
        Html = options.OutputHtmlPath,
        Thresholds = !string.IsNullOrWhiteSpace(options.ThresholdsPath)
                ? options.ThresholdsPath
                : !string.IsNullOrWhiteSpace(options.ThresholdsJson) ? "(inline thresholds)" : null
      },
      BaselineReference = options.BaselineReference,
      SuppressedSymbols = suppressedSymbols
    };
  }

  /// <summary>
  /// Builds the metrics report and logs any errors.
  /// </summary>
  /// <param name="aggregationInput">The input data for aggregation.</param>
  /// <param name="options">The options containing filter configuration.</param>
  /// <param name="logger">The logger to use for error messages.</param>
  /// <returns>The built report, or <see langword="null"/> if building failed.</returns>
  private static MetricsReport? BuildReportWithLogging(
      MetricsAggregationInput aggregationInput,
      MetricsReporterOptions options,
      FileLogger logger)
  {
    var memberFilter = MemberFilter.FromString(options.ExcludedMemberNamesPatterns);
    var assemblyFilter = AssemblyFilter.FromString(options.ExcludedAssemblyNames);
    var typeFilter = TypeFilter.FromString(options.ExcludedTypeNamePatterns);
    var aggregationService = new MetricsAggregationService(memberFilter, assemblyFilter, typeFilter);

    try
    {
      return aggregationService.BuildReport(aggregationInput);
    }
    catch (Exception ex)
    {
      logger.LogError("Failed to build metrics report.", ex);
      return null;
    }
  }

  /// <summary>
  /// Writes the generated reports to output files.
  /// </summary>
  /// <param name="report">The metrics report to write.</param>
  /// <param name="options">The options containing output paths.</param>
  /// <param name="logger">The logger to use for error messages.</param>
  /// <param name="cancellationToken">Cancellation token for async operations.</param>
  /// <returns>The exit code indicating success or failure.</returns>
  private async Task<MetricsReporterExitCode> WriteReportsAsync(
      MetricsReport report,
      MetricsReporterOptions options,
      FileLogger logger,
      CancellationToken cancellationToken)
  {
    try
    {
      await ReportWriter.WriteJsonAsync(report, options.OutputJsonPath, cancellationToken).ConfigureAwait(false);
      if (!string.IsNullOrWhiteSpace(options.OutputHtmlPath))
      {
        var html = HtmlReportGenerator.Generate(report, options.CoverageHtmlDir);
        await ReportWriter.WriteHtmlAsync(html, options.OutputHtmlPath, cancellationToken).ConfigureAwait(false);
      }

      return MetricsReporterExitCode.Success;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      logger.LogError("Failed to write output files.", ex);
      return MetricsReporterExitCode.IoError;
    }
  }

  private static void ValidateOptions(MetricsReporterOptions options)
  {
    // Skip validation if using input JSON mode
    if (!string.IsNullOrWhiteSpace(options.InputJsonPath))
    {
      return;
    }

    if (string.IsNullOrWhiteSpace(options.OutputJsonPath))
    {
      throw new ArgumentException("Output JSON path is required.", nameof(options));
    }

    if (string.IsNullOrWhiteSpace(options.MetricsDirectory))
    {
      throw new ArgumentException("Metrics directory is required.", nameof(options));
    }

    foreach (var path in EnumerateInputFiles(options))
    {
      if (!File.Exists(path))
      {
        throw new FileNotFoundException($"Input file not found: {path}", path);
      }
    }

    if (!string.IsNullOrWhiteSpace(options.OutputHtmlPath))
    {
      var htmlDir = Path.GetDirectoryName(options.OutputHtmlPath);
      if (!string.IsNullOrWhiteSpace(htmlDir) && !Directory.Exists(htmlDir))
      {
        Directory.CreateDirectory(htmlDir);
      }
    }
  }

  private static IEnumerable<string> EnumerateInputFiles(MetricsReporterOptions options)
  {
    if (!string.IsNullOrWhiteSpace(options.AltCoverPath))
    {
      yield return options.AltCoverPath;
    }

    foreach (var path in options.RoslynPaths)
    {
      yield return path;
    }

    foreach (var path in options.SarifPaths)
    {
      yield return path;
    }
  }

  private IDictionary<MetricIdentifier, MetricThresholdDefinition> ParseThresholds(
      MetricsReporterOptions options)
  {
    string? payload = null;

    if (!string.IsNullOrWhiteSpace(options.ThresholdsPath))
    {
      var absolutePath = Path.GetFullPath(options.ThresholdsPath);
      if (!File.Exists(absolutePath))
      {
        throw new FileNotFoundException($"Thresholds file not found: {absolutePath}", absolutePath);
      }

      payload = File.ReadAllText(absolutePath);
    }
    else
    {
      payload = options.ThresholdsJson;
    }

    return ThresholdsParser.Parse(payload);
  }

  private static async Task<ParsedMetricsDocument?> ParseSafeAsync(
      IMetricsSourceParser parser,
      string path,
      FileLogger logger,
      CancellationToken cancellationToken)
  {
    try
    {
      logger.LogInformation($"Parsing metrics: {path}");
      return await parser.ParseAsync(path, cancellationToken).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      logger.LogError($"Failed to parse metrics file: {path}", ex);
      return null;
    }
  }

  /// <summary>
  /// Loads an existing JSON report and generates HTML from it without parsing source files.
  /// </summary>
  private async Task<MetricsReporterExitCode> GenerateHtmlFromJsonAsync(
      MetricsReporterOptions options,
      FileLogger logger,
      CancellationToken cancellationToken)
  {
    var validationResult = ValidateHtmlGenerationOptions(options, logger);
    if (validationResult != MetricsReporterExitCode.Success)
    {
      return validationResult;
    }

    try
    {
      var report = await LoadReportForHtmlGenerationAsync(options, logger, cancellationToken).ConfigureAwait(false);
      if (report is null)
      {
        return MetricsReporterExitCode.ValidationError;
      }

      return await GenerateAndWriteHtmlAsync(report, options, logger, cancellationToken).ConfigureAwait(false);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      logger.LogError("Failed to write HTML output file.", ex);
      return MetricsReporterExitCode.IoError;
    }
  }

  /// <summary>
  /// Validates options required for HTML generation from JSON.
  /// </summary>
  /// <param name="options">The options to validate.</param>
  /// <param name="logger">The logger to use for error messages.</param>
  /// <returns>The exit code indicating validation result.</returns>
  private static MetricsReporterExitCode ValidateHtmlGenerationOptions(MetricsReporterOptions options, FileLogger logger)
  {
    if (string.IsNullOrWhiteSpace(options.InputJsonPath))
    {
      logger.LogError("Input JSON path is required for HTML-only generation.");
      return MetricsReporterExitCode.ValidationError;
    }

    if (string.IsNullOrWhiteSpace(options.OutputHtmlPath))
    {
      logger.LogError("Output HTML path is required for HTML-only generation.");
      return MetricsReporterExitCode.ValidationError;
    }

    return MetricsReporterExitCode.Success;
  }

  /// <summary>
  /// Loads a metrics report from JSON for HTML generation.
  /// </summary>
  /// <param name="options">The options containing the JSON file path.</param>
  /// <param name="logger">The logger to use for progress messages.</param>
  /// <param name="cancellationToken">Cancellation token for async operations.</param>
  /// <returns>The loaded metrics report, or <see langword="null"/> if loading failed.</returns>
  private async Task<MetricsReport?> LoadReportForHtmlGenerationAsync(
      MetricsReporterOptions options,
      FileLogger logger,
      CancellationToken cancellationToken)
  {
    try
    {
      logger.LogInformation($"Loading metrics report from: {options.InputJsonPath}");
      var report = await JsonReportLoader.LoadAsync(options.InputJsonPath!, cancellationToken).ConfigureAwait(false);

      if (report is null)
      {
        logger.LogError("Failed to deserialize metrics report from JSON.");
        return null;
      }

      return report;
    }
    catch (FileNotFoundException ex)
    {
      logger.LogError($"Input JSON file not found: {ex.Message}", ex);
      return null;
    }
    catch (Exception ex)
    {
      logger.LogError($"Failed to load JSON report: {ex.Message}", ex);
      return null;
    }
  }

  /// <summary>
  /// Generates HTML from a metrics report and writes it to disk.
  /// </summary>
  /// <param name="report">The metrics report to generate HTML from.</param>
  /// <param name="options">The options containing output path.</param>
  /// <param name="logger">The logger to use for progress messages.</param>
  /// <param name="cancellationToken">Cancellation token for async operations.</param>
  /// <returns>The exit code indicating success or failure.</returns>
  private async Task<MetricsReporterExitCode> GenerateAndWriteHtmlAsync(
      MetricsReport report,
      MetricsReporterOptions options,
      FileLogger logger,
      CancellationToken cancellationToken)
  {
    logger.LogInformation("Generating HTML report...");
    var html = HtmlReportGenerator.Generate(report);

    await ReportWriter.WriteHtmlAsync(html, options.OutputHtmlPath, cancellationToken).ConfigureAwait(false);
    logger.LogInformation($"HTML report generated successfully: {options.OutputHtmlPath}");
    return MetricsReporterExitCode.Success;
  }

}

