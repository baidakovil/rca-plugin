namespace Rca.Tools.MetricsReporter.Services;

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Rca.Tools.MetricsReporter.Model;
using Rca.Tools.MetricsReporter.Serialization;

/// <summary>
/// Persists JSON and HTML reports to disk.
/// </summary>
public sealed class ReportWriter
{
  /// <summary>
  /// Writes the JSON report to disk.
  /// </summary>
  public async Task WriteJsonAsync(MetricsReport report, string path, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(report);
    ArgumentException.ThrowIfNullOrWhiteSpace(path);

    EnsureDirectory(path);

    await using var stream = File.Create(path);
    await JsonSerializer.SerializeAsync(stream, report, JsonSerializerOptionsFactory.Create(), cancellationToken).ConfigureAwait(false);
  }

  /// <summary>
  /// Writes the HTML representation of the report to disk.
  /// </summary>
  public async Task WriteHtmlAsync(string html, string path, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(html);
    ArgumentException.ThrowIfNullOrWhiteSpace(path);

    EnsureDirectory(path);
    await File.WriteAllTextAsync(path, html, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
  }

  private static void EnsureDirectory(string path)
  {
    var directory = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(directory))
    {
      Directory.CreateDirectory(directory);
    }
  }
}

