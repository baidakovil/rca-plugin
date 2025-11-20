namespace Rca.Tools.MetricsReporter.Processing;

using System.Collections.Generic;

/// <summary>
/// Represents the parsing result of a single metrics source.
/// </summary>
public sealed class ParsedMetricsDocument
{
  /// <summary>
  /// Solution name supplied by the source or by command line arguments.
  /// </summary>
  public string SolutionName { get; init; } = string.Empty;

  /// <summary>
  /// Code elements discovered in the source.
  /// </summary>
  public IList<ParsedCodeElement> Elements { get; init; } = [];
}

