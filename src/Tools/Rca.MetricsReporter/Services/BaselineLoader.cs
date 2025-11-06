namespace Rca.Tools.MetricsReporter.Services;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Rca.Tools.MetricsReporter.Model;
using Rca.Tools.MetricsReporter.Serialization;

/// <summary>
/// Загружает baseline отчёт из JSON файла.
/// </summary>
public sealed class BaselineLoader
{
    /// <summary>
    /// Асинхронно загружает baseline отчёт.
    /// </summary>
    /// <param name="path">Путь к baseline файлу. Может быть <see langword="null"/>.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Загруженный baseline или <see langword="null"/>.</returns>
    public async Task<MetricsReport?> LoadAsync(string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<MetricsReport>(stream, JsonSerializerOptionsFactory.Create(), cancellationToken).ConfigureAwait(false);
    }
}

