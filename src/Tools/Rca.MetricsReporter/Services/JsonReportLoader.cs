namespace Rca.Tools.MetricsReporter.Services;

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Rca.Tools.MetricsReporter.Model;
using Rca.Tools.MetricsReporter.Serialization;

/// <summary>
/// Loads metrics reports from JSON files.
/// </summary>
public sealed class JsonReportLoader
{
  /// <summary>
  /// Loads a metrics report from a JSON file.
  /// </summary>
  /// <param name="jsonPath">Path to the JSON file containing the metrics report.</param>
  /// <param name="cancellationToken">Cancellation token for async operations.</param>
  /// <returns>The loaded metrics report, or <see langword="null"/> if deserialization failed.</returns>
  /// <exception cref="FileNotFoundException">Thrown when the JSON file does not exist.</exception>
  /// <exception cref="JsonException">Thrown when the JSON content is invalid.</exception>
  public async Task<MetricsReport?> LoadAsync(string jsonPath, CancellationToken cancellationToken)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(jsonPath);

    if (!File.Exists(jsonPath))
    {
      throw new FileNotFoundException($"JSON file not found: {jsonPath}", jsonPath);
    }

    await using var stream = File.OpenRead(jsonPath);
    return await JsonSerializer.DeserializeAsync<MetricsReport>(
        stream,
        JsonSerializerOptionsFactory.Create(),
        cancellationToken).ConfigureAwait(false);
  }
}

