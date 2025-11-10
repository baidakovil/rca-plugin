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

        try
        {
            ValidateOptions(options);
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message, ex);
            return MetricsReporterExitCode.ValidationError;
        }

        IDictionary<MetricIdentifier, MetricThreshold> thresholds;
        try
        {
            thresholds = ParseThresholds(options.ThresholdsJson);
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message, ex);
            return MetricsReporterExitCode.ValidationError;
        }
        var baseline = await _baselineLoader.LoadAsync(options.BaselinePath, cancellationToken).ConfigureAwait(false);

        var altCoverDocuments = new List<ParsedMetricsDocument>();
        if (!string.IsNullOrWhiteSpace(options.AltCoverPath))
        {
            var document = await ParseSafeAsync(_altCoverParser, options.AltCoverPath, logger, cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                return MetricsReporterExitCode.ParsingError;
            }

            altCoverDocuments.Add(document);
        }

        var roslynDocuments = new List<ParsedMetricsDocument>();
        foreach (var path in options.RoslynPaths)
        {
            var document = await ParseSafeAsync(_roslynParser, path, logger, cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                return MetricsReporterExitCode.ParsingError;
            }

            roslynDocuments.Add(document);
        }

        var sarifDocuments = new List<ParsedMetricsDocument>();
        foreach (var path in options.SarifPaths)
        {
            var document = await ParseSafeAsync(_sarifParser, path, logger, cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                return MetricsReporterExitCode.ParsingError;
            }

            sarifDocuments.Add(document);
        }

        var memberFilter = MemberFilter.FromString(options.ExcludedMethodNames);
        var assemblyFilter = AssemblyFilter.FromString(options.ExcludedAssemblyNames);
        var aggregationService = new MetricsAggregationService(memberFilter, assemblyFilter);

        var aggregationInput = new MetricsAggregationInput
        {
            SolutionName = options.SolutionName,
            AltCoverDocuments = altCoverDocuments,
            RoslynDocuments = roslynDocuments,
            SarifDocuments = sarifDocuments,
            Baseline = baseline,
            Thresholds = thresholds,
            Paths = new ReportPaths
            {
                MetricsDirectory = options.MetricsDirectory,
                Baseline = options.BaselinePath,
                Report = options.OutputJsonPath,
                Html = options.OutputHtmlPath
            },
            BaselineReference = options.BaselineReference
        };

        MetricsReport report;
        try
        {
            report = aggregationService.BuildReport(aggregationInput);
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to build metrics report.", ex);
            return MetricsReporterExitCode.ValidationError;
        }

        try
        {
            await _reportWriter.WriteJsonAsync(report, options.OutputJsonPath, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(options.OutputHtmlPath))
            {
                var html = _htmlGenerator.Generate(report);
                await _reportWriter.WriteHtmlAsync(html, options.OutputHtmlPath, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError("Failed to write output files.", ex);
            return MetricsReporterExitCode.IoError;
        }

        logger.LogInformation("Metrics Reporter completed successfully.");
        return MetricsReporterExitCode.Success;
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

    private IDictionary<MetricIdentifier, MetricThreshold> ParseThresholds(string? thresholdsJson)
        => _thresholdsParser.Parse(thresholdsJson);

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
}

