namespace Rca.Tools.MetricsReporter.MetricsReader.Services;
using Rca.Tools.MetricsReporter.MetricsReader.Settings;
/// <summary>
/// Parameters for ordering symbol metric snapshots.
/// </summary>
internal sealed record SymbolSnapshotOrderingParameters(
  MetricsReaderSymbolKind SymbolKind);



