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
    private readonly ThresholdsParser _thresholdsParser = new();
    private readonly BaselineLoader _baselineLoader = new();
    private readonly BaselineManager _baselineManager = new();
    private readonly MetricsAggregationService _aggregationService;
    private readonly HtmlReportGenerator _htmlGenerator = new();
    private readonly ReportWriter _reportWriter = new();

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
        _aggregationService = new MetricsAggregationService(memberFilter, new AssemblyFilter());
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

        var baseline = await _baselineLoader.LoadAsync(options.BaselinePath, cancellationToken).ConfigureAwait(false);

        var documentsResult = await ParseAllDocumentsAsync(options, logger, cancellationToken).ConfigureAwait(false);
        if (documentsResult.ExitCode != MetricsReporterExitCode.Success)
        {
            return documentsResult.ExitCode;
        }

        var aggregationInput = BuildAggregationInput(options, documentsResult, thresholdsResult.Thresholds, baseline);
        var report = BuildReportWithLogging(aggregationInput, options, logger);
        if (report is null)
        {
            return MetricsReporterExitCode.ValidationError;
        }

        // Write initial report to output location
        var writeResult = await WriteReportsAsync(report, options, logger, cancellationToken).ConfigureAwait(false);
        if (writeResult != MetricsReporterExitCode.Success)
        {
            return writeResult;
        }

        // Handle baseline replacement if enabled
        if (options.ReplaceMetricsBaseline && !string.IsNullOrWhiteSpace(options.BaselinePath))
        {
            var baselineReplaced = await HandleBaselineReplacementAsync(options, logger, cancellationToken).ConfigureAwait(false);
            
            // If baseline was replaced, regenerate report with new baseline for correct deltas
            if (baselineReplaced)
            {
                logger.LogInformation("Regenerating report with new baseline for accurate delta calculation...");
                var newBaseline = await _baselineLoader.LoadAsync(options.BaselinePath, cancellationToken).ConfigureAwait(false);
                var updatedAggregationInput = BuildAggregationInput(options, documentsResult, thresholdsResult.Thresholds, newBaseline);
                var updatedReport = BuildReportWithLogging(updatedAggregationInput, options, logger);
                
                if (updatedReport is null)
                {
                    logger.LogError("Failed to regenerate report after baseline replacement.");
                    return MetricsReporterExitCode.ValidationError;
                }

                // Rewrite reports with updated deltas
                writeResult = await WriteReportsAsync(updatedReport, options, logger, cancellationToken).ConfigureAwait(false);
                if (writeResult != MetricsReporterExitCode.Success)
                {
                    return writeResult;
                }
            }
        }

        logger.LogInformation("Metrics Reporter completed successfully.");
        return MetricsReporterExitCode.Success;
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
        MetricsReport? baseline)
    {
        var memberFilter = MemberFilter.FromString(options.ExcludedMethodNames);
        var assemblyFilter = AssemblyFilter.FromString(options.ExcludedAssemblyNames);

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
            BaselineReference = options.BaselineReference
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
        var memberFilter = MemberFilter.FromString(options.ExcludedMethodNames);
        var assemblyFilter = AssemblyFilter.FromString(options.ExcludedAssemblyNames);
        var aggregationService = new MetricsAggregationService(memberFilter, assemblyFilter);

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
            await _reportWriter.WriteJsonAsync(report, options.OutputJsonPath, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(options.OutputHtmlPath))
            {
                var html = _htmlGenerator.Generate(report);
                await _reportWriter.WriteHtmlAsync(html, options.OutputHtmlPath, cancellationToken).ConfigureAwait(false);
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

        return _thresholdsParser.Parse(payload);
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

        if (!File.Exists(options.InputJsonPath))
        {
            logger.LogError($"Input JSON file not found: {options.InputJsonPath}");
            return MetricsReporterExitCode.ValidationError;
        }

        try
        {
            logger.LogInformation($"Loading metrics report from: {options.InputJsonPath}");
            await using var stream = File.OpenRead(options.InputJsonPath);
            var report = await JsonSerializer.DeserializeAsync<MetricsReport>(
                stream,
                JsonSerializerOptionsFactory.Create(),
                cancellationToken).ConfigureAwait(false);

            if (report is null)
            {
                logger.LogError("Failed to deserialize metrics report from JSON.");
                return MetricsReporterExitCode.ValidationError;
            }

            logger.LogInformation("Generating HTML report...");
            var html = _htmlGenerator.Generate(report);
            
            var htmlDir = Path.GetDirectoryName(options.OutputHtmlPath);
            if (!string.IsNullOrWhiteSpace(htmlDir) && !Directory.Exists(htmlDir))
            {
                Directory.CreateDirectory(htmlDir);
            }

            await _reportWriter.WriteHtmlAsync(html, options.OutputHtmlPath, cancellationToken).ConfigureAwait(false);
            logger.LogInformation($"HTML report generated successfully: {options.OutputHtmlPath}");
            return MetricsReporterExitCode.Success;
        }
        catch (JsonException ex)
        {
            logger.LogError($"Failed to deserialize JSON report: {ex.Message}", ex);
            return MetricsReporterExitCode.ValidationError;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError("Failed to write HTML output file.", ex);
            return MetricsReporterExitCode.IoError;
        }
    }

    /// <summary>
    /// Handles baseline replacement by comparing the new report with existing baseline.
    /// </summary>
    /// <param name="options">The metrics reporter options containing paths and configuration.</param>
    /// <param name="logger">Logger instance for recording operations.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// <see langword="true"/> if baseline was replaced; <see langword="false"/> if replacement was not needed.
    /// </returns>
    /// <remarks>
    /// This method compares the newly generated metrics-report.json with the existing metrics-baseline.json.
    /// If they differ, it archives the old baseline to storage and replaces it with the new report.
    /// </remarks>
    private async Task<bool> HandleBaselineReplacementAsync(
        MetricsReporterOptions options,
        FileLogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options.BaselinePath);

        try
        {
            // Compare new report with existing baseline
            var areDifferent = await _baselineManager.AreFilesDifferentAsync(
                options.OutputJsonPath,
                options.BaselinePath,
                cancellationToken).ConfigureAwait(false);

            if (!areDifferent)
            {
                logger.LogInformation("New report is identical to existing baseline. Baseline replacement skipped.");
                return false;
            }

            logger.LogInformation("New report differs from existing baseline. Proceeding with baseline replacement...");

            // Replace baseline: archive old one and copy new report to baseline location
            var replaced = await _baselineManager.ReplaceBaselineAsync(
                options.OutputJsonPath,
                options.BaselinePath,
                options.MetricsReportStoragePath,
                logger,
                cancellationToken).ConfigureAwait(false);

            if (replaced)
            {
                logger.LogInformation($"Baseline successfully replaced at: {options.BaselinePath}");
            }

            return replaced;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error during baseline replacement: {ex.Message}", ex);
            return false;
        }
    }
}

