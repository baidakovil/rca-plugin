namespace Rca.Tools.MetricsReporter.MetricsReader.Commands;

using System.Threading.Tasks;
using System.Threading;
using Rca.Tools.MetricsReporter.MetricsReader.Output;
using Rca.Tools.MetricsReporter.MetricsReader.Services;
using Rca.Tools.MetricsReporter.MetricsReader.Settings;
using Rca.Tools.MetricsReporter.Model;
using Spectre.Console.Cli;

/// <summary>
/// Provides helpers shared by all metrics-reader commands.
/// </summary>
internal abstract class MetricsReaderCommandBase<TSettings> : AsyncCommand<TSettings>
  where TSettings : MetricsReaderSettingsBase
{
  protected static async Task<MetricsReaderEngine> CreateEngineAsync(TSettings settings, CancellationToken cancellationToken)
  {
    using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, MetricsReaderCancellation.Token);
    var context = await MetricsReaderContextFactory.CreateAsync(settings, linkedSource.Token).ConfigureAwait(false);
    return new MetricsReaderEngine(context);
  }
}

