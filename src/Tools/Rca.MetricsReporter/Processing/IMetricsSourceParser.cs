namespace Rca.Tools.MetricsReporter.Processing;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Унифицированный контракт для парсеров исходных метрик.
/// </summary>
public interface IMetricsSourceParser
{
    /// <summary>
    /// Выполняет асинхронный парсинг файла метрик.
    /// </summary>
    /// <param name="path">Путь к файлу источника.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Результат парсинга.</returns>
    Task<ParsedMetricsDocument> ParseAsync(string path, CancellationToken cancellationToken);
}

