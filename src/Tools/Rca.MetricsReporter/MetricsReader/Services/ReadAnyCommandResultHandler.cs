namespace Rca.Tools.MetricsReporter.MetricsReader.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using Rca.Tools.MetricsReporter.MetricsReader.Output;

/// <summary>
/// Handles result formatting and output for ReadAny command.
/// </summary>
internal sealed class ReadAnyCommandResultHandler : IReadAnyCommandResultHandler
{
  /// <inheritdoc/>
  public void HandleResults(IEnumerable<SymbolMetricSnapshot> snapshots, ReadAnyCommandResultParameters parameters)
  {
    ArgumentNullException.ThrowIfNull(snapshots);
    ArgumentNullException.ThrowIfNull(parameters);

    var result = snapshots
      .Select(SymbolMetricDto.FromSnapshot)
      .ToList();

    if (result.Count == 0)
    {
      var noViolationsDto = new NoViolationsFoundDto(
        parameters.Metric,
        parameters.Namespace,
        parameters.SymbolKind,
        $"No violations were found for metric '{parameters.Metric}' in namespace '{parameters.Namespace}'.");
      JsonConsoleWriter.Write(noViolationsDto);
      return;
    }

    if (parameters.ShowAll)
    {
      JsonConsoleWriter.Write(result);
      return;
    }

    JsonConsoleWriter.Write(result.FirstOrDefault());
  }
}

