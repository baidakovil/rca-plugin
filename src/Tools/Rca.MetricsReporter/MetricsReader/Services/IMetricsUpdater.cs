namespace Rca.Tools.MetricsReporter.MetricsReader.Services;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Updates metrics reports by running MSBuild targets.
/// </summary>
internal interface IMetricsUpdater
{
  /// <summary>
  /// Updates the metrics report by running the GenerateMetricsDashboard MSBuild target.
  /// </summary>
  /// <param name="cancellationToken">Cancellation token for async operations.</param>
  Task UpdateAsync(CancellationToken cancellationToken);
}

