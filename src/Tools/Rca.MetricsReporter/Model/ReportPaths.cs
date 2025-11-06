namespace Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Содержит ключевые пути, используемые в отчёте.
/// </summary>
public sealed class ReportPaths
{
    /// <summary>
    /// Абсолютный или относительный путь к каталогу с метриками.
    /// </summary>
    public string MetricsDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Путь к baseline файлу.
    /// </summary>
    public string? Baseline { get; init; }
        = null;

    /// <summary>
    /// Путь к текущему JSON отчёту.
    /// </summary>
    public string Report { get; init; } = string.Empty;

    /// <summary>
    /// Путь к HTML-дашборду.
    /// </summary>
    public string Html { get; init; } = string.Empty;
}

