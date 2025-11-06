namespace Rca.Tools.MetricsReporter;

/// <summary>
/// Код завершения консольного агрегатора.
/// </summary>
public enum MetricsReporterExitCode
{
    /// <summary>
    /// Выполнение успешно завершено.
    /// </summary>
    Success = 0,

    /// <summary>
    /// Ошибка парсинга входных файлов.
    /// </summary>
    ParsingError = 1,

    /// <summary>
    /// Ошибка ввода-вывода.
    /// </summary>
    IoError = 2,

    /// <summary>
    /// Ошибка валидации входных параметров или несогласованности данных.
    /// </summary>
    ValidationError = 3
}

