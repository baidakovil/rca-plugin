namespace Rca.Tools.MetricsReporter.Services;

using System.Collections.Generic;

/// <summary>
/// Параметры запуска агрегатора метрик.
/// </summary>
public sealed class MetricsReporterOptions
{
    /// <summary>
    /// Имя solution для отображения в отчёте.
    /// </summary>
    public string SolutionName { get; init; } = "Solution";

    /// <summary>
    /// Путь к файлу AltCover/OpenCover coverage.xml.
    /// </summary>
    public string? AltCoverPath { get; init; }

    /// <summary>
    /// Пути к XML отчётам Roslyn Code Metrics.
    /// </summary>
    public IReadOnlyCollection<string> RoslynPaths { get; init; } = new List<string>();

    /// <summary>
    /// Пути к SARIF файлам.
    /// </summary>
    public IReadOnlyCollection<string> SarifPaths { get; init; } = new List<string>();

    /// <summary>
    /// Путь к baseline JSON.
    /// </summary>
    public string? BaselinePath { get; init; }

    /// <summary>
    /// Текстовая пометка baseline (commit, build и т.п.).
    /// </summary>
    public string? BaselineReference { get; init; }

    /// <summary>
    /// Строка с пороговыми значениями в формате JSON.
    /// </summary>
    public string? ThresholdsJson { get; init; }

    /// <summary>
    /// Путь к итоговому JSON.
    /// </summary>
    public string OutputJsonPath { get; init; } = string.Empty;

    /// <summary>
    /// Путь к итоговому HTML.
    /// </summary>
    public string OutputHtmlPath { get; init; } = string.Empty;

    /// <summary>
    /// Каталог метрик (MetricsDir).
    /// </summary>
    public string MetricsDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Путь к лог-файлу.
    /// </summary>
    public string LogFilePath { get; init; } = string.Empty;
}

