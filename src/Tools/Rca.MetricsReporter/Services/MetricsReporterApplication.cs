namespace Rca.Tools.MetricsReporter.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rca.Tools.MetricsReporter.Aggregation;
using Rca.Tools.MetricsReporter.Configuration;
using Rca.Tools.MetricsReporter.Logging;
using Rca.Tools.MetricsReporter.Model;
using Rca.Tools.MetricsReporter.Processing;
using Rca.Tools.MetricsReporter.Processing.Parsers;
using Rca.Tools.MetricsReporter.Rendering;

/// <summary>
/// Высокоуровневая координация агрегации и генерации отчёта.
/// </summary>
public sealed class MetricsReporterApplication
{
    private readonly AltCoverMetricsParser _altCoverParser = new();
    private readonly RoslynMetricsParser _roslynParser = new();
    private readonly SarifMetricsParser _sarifParser = new();
    private readonly ThresholdsParser _thresholdsParser = new();
    private readonly BaselineLoader _baselineLoader = new();
    private readonly MetricsAggregationService _aggregationService = new();
    private readonly HtmlReportGenerator _htmlGenerator = new();
    private readonly ReportWriter _reportWriter = new();

    /// <summary>
    /// Запускает процесс агрегации.
    /// </summary>
    public async Task<MetricsReporterExitCode> RunAsync(MetricsReporterOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        using var logger = new FileLogger(options.LogFilePath);
        logger.LogInformation("Metrics Reporter started.");

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
            report = _aggregationService.BuildReport(aggregationInput);
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to build metrics report.", ex);
            return MetricsReporterExitCode.ValidationError;
        }

        try
        {
            await _reportWriter.WriteJsonAsync(report, options.OutputJsonPath, cancellationToken).ConfigureAwait(false);
            var html = _htmlGenerator.Generate(report);
            await _reportWriter.WriteHtmlAsync(html, options.OutputHtmlPath, cancellationToken).ConfigureAwait(false);
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
        if (string.IsNullOrWhiteSpace(options.OutputJsonPath))
        {
            throw new ArgumentException("Output JSON path is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.OutputHtmlPath))
        {
            throw new ArgumentException("Output HTML path is required.", nameof(options));
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
}

