namespace Rca.Tools.MetricsReporter.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rca.Tools.MetricsReporter.Aggregation;
using Rca.Tools.MetricsReporter.Logging;
using Rca.Tools.MetricsReporter.Model;
using Rca.Tools.MetricsReporter.Processing;
using Rca.Tools.MetricsReporter.Processing.Parsers;
using Rca.Tools.MetricsReporter.Rendering;
using Rca.Tools.MetricsReporter.Serialization;

/// <summary>
/// Handles parsing metrics sources, building aggregation input, and writing final reports.
/// </summary>
internal sealed class MetricsReportPipeline
{
  private readonly AltCoverMetricsParser _altCoverParser;
  private readonly RoslynMetricsParser _roslynParser;
  private readonly SarifMetricsParser _sarifParser;

  public MetricsReportPipeline()
    : this(new AltCoverMetricsParser(), new RoslynMetricsParser(), new SarifMetricsParser())
  {
  }

  internal MetricsReportPipeline(
      AltCoverMetricsParser altCoverParser,
      RoslynMetricsParser roslynParser,
      SarifMetricsParser sarifParser)
  {
    _altCoverParser = altCoverParser;
    _roslynParser = roslynParser;
    _sarifParser = sarifParser;
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
      "Microsoft.Maintainability",
      "CA1506:Avoid excessive class coupling",
      Justification = "Pipeline orchestrator coordinates document parsing, report generation, and report writing; further decomposition would require wrapper methods which are prohibited by refactoring rules.")]
  public async Task<MetricsReporterExitCode> ExecuteAsync(
      MetricsReporterOptions options,
      ThresholdLoadResult thresholdsResult,
      MetricsReport? baseline,
      List<SuppressedSymbolInfo> suppressedSymbols,
      FileLogger logger,
      CancellationToken cancellationToken)
  {
    var documentsResult = await ParseAllDocumentsAsync(options, logger, cancellationToken).ConfigureAwait(false);
    if (documentsResult.ExitCode != MetricsReporterExitCode.Success)
    {
      return documentsResult.ExitCode;
    }

    var report = GenerateReport(options, documentsResult, thresholdsResult, baseline, suppressedSymbols, logger);
    if (report is null)
    {
      return MetricsReporterExitCode.ValidationError;
    }

    return await WriteReportsAsync(report, options, logger, cancellationToken).ConfigureAwait(false);
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
      "Microsoft.Maintainability",
      "CA1506:Avoid excessive class coupling",
      Justification = "Report generation method coordinates multiple services and data structures; further decomposition would require wrapper methods which are prohibited by refactoring rules.")]
  private static MetricsReport? GenerateReport(
      MetricsReporterOptions options,
      (MetricsReporterExitCode ExitCode, IList<ParsedMetricsDocument> AltCoverDocuments, IList<ParsedMetricsDocument> RoslynDocuments, IList<ParsedMetricsDocument> SarifDocuments) documentsResult,
      ThresholdLoadResult thresholdsResult,
      MetricsReport? baseline,
      List<SuppressedSymbolInfo> suppressedSymbols,
      FileLogger logger)
  {
    var aggregationInput = BuildAggregationInput(options, documentsResult, thresholdsResult.Configuration.AsDictionary(), baseline, suppressedSymbols);
    return BuildReportWithLogging(aggregationInput, options, logger);
  }

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

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
      "Microsoft.Maintainability",
      "CA1506:Avoid excessive class coupling",
      Justification = "Method constructs aggregation input object from multiple sources; further decomposition would require wrapper methods which are prohibited by refactoring rules.")]
  private static MetricsAggregationInput BuildAggregationInput(
      MetricsReporterOptions options,
      (MetricsReporterExitCode ExitCode, IList<ParsedMetricsDocument> AltCoverDocuments, IList<ParsedMetricsDocument> RoslynDocuments, IList<ParsedMetricsDocument> SarifDocuments) documentsResult,
      IDictionary<MetricIdentifier, MetricThresholdDefinition> thresholds,
      MetricsReport? baseline,
      List<SuppressedSymbolInfo> suppressedSymbols)
  {
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

  private static async Task<MetricsReporterExitCode> WriteReportsAsync(
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

