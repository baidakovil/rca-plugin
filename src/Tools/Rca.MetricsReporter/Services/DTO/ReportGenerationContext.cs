namespace Rca.Tools.MetricsReporter.Services.DTO;

using Rca.Tools.MetricsReporter.Configuration;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Context for report generation containing all necessary parameters.
/// </summary>
/// <param name="Options">Metrics reporter options.</param>
/// <param name="ThresholdsResult">Loaded threshold configuration result.</param>
/// <param name="BaselineContext">Baseline run context snapshot.</param>
internal sealed record ReportGenerationContext(
    MetricsReporterOptions Options,
    ThresholdLoadResult ThresholdsResult,
    BaselineRunContext BaselineContext);

