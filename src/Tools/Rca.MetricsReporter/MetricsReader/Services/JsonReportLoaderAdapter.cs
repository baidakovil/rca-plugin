namespace Rca.Tools.MetricsReporter.MetricsReader.Services;

using System.Threading;
using System.Threading.Tasks;
using Rca.Tools.MetricsReporter.Model;
using Rca.Tools.MetricsReporter.Services;

/// <summary>
/// Adapter that wraps the static JsonReportLoader to implement IJsonReportLoader interface.
/// </summary>
internal sealed class JsonReportLoaderAdapter : IJsonReportLoader
{
  /// <inheritdoc/>
  public Task<MetricsReport?> LoadAsync(string jsonPath, CancellationToken cancellationToken)
    => JsonReportLoader.LoadAsync(jsonPath, cancellationToken);
}

