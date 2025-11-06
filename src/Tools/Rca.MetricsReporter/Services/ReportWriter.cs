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
/// Сохраняет JSON и HTML отчёты на диск.
/// </summary>
public sealed class ReportWriter
{
    /// <summary>
    /// Сохраняет JSON отчёт.
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
    /// Сохраняет HTML представление отчёта.
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

