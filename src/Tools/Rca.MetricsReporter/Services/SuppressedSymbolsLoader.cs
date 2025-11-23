namespace Rca.Tools.MetricsReporter.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Rca.Tools.MetricsReporter.Model;
using Rca.Tools.MetricsReporter.Serialization;

/// <summary>
/// Loads suppressed symbol metadata from <c>RcaSuppressedSymbols.json</c> when present.
/// </summary>
/// <remarks>
/// This helper keeps JSON handling for suppressed symbols localized and reuses the
/// same <see cref="System.Text.Json.JsonSerializerOptions"/> as the main report to
/// guarantee consistent casing and enum handling.
/// </remarks>
internal static class SuppressedSymbolsLoader
{
  /// <summary>
  /// Loads suppressed symbol entries from the specified JSON file if it exists.
  /// </summary>
  /// <param name="path">Path to <c>RcaSuppressedSymbols.json</c> or <see langword="null"/>.</param>
  /// <param name="cancellationToken">Cancellation token for I/O operations.</param>
  /// <returns>
  /// A list of <see cref="SuppressedSymbolInfo"/> instances. Returns an empty list when
  /// the path is <see langword="null"/>, empty, or the file is missing.
  /// </returns>
  public static async Task<IList<SuppressedSymbolInfo>> LoadAsync(string? path, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
    {
      return new List<SuppressedSymbolInfo>();
    }

    await using var stream = File.OpenRead(path);
    var options = JsonSerializerOptionsFactory.Create();
    var report = await System.Text.Json.JsonSerializer
        .DeserializeAsync<SuppressedSymbolsReport>(stream, options, cancellationToken)
        .ConfigureAwait(false);

    return report?.SuppressedSymbols ?? new List<SuppressedSymbolInfo>();
  }
}


