namespace Rca.Tools.MetricsReporter.MetricsReader.Services;

using System.Collections.Generic;
using Rca.Tools.MetricsReporter.Configuration;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Adapter that wraps the static ThresholdsParser to implement IThresholdsParser interface.
/// </summary>
internal sealed class ThresholdsParserAdapter : IThresholdsParser
{
  /// <inheritdoc/>
  public IReadOnlyDictionary<MetricIdentifier, MetricThresholdDefinition> Parse(string? input)
    => ThresholdsParser.Parse(input);
}

