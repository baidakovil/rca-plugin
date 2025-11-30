namespace Rca.Tools.MetricsReporter.Processing.Parsers;

using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Represents the normalized source location for a SARIF violation.
/// </summary>
internal sealed record SarifLocation(SourceLocation Source, string? OriginalUri);

