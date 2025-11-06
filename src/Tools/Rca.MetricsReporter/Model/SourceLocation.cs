namespace Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Описывает исходный файл и диапазон строк для узла отчёта.
/// </summary>
public sealed class SourceLocation
{
    /// <summary>
    /// Путь к файлу относительно корня решения.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Первая строка, охватываемая узлом, или <see langword="null"/> если неизвестно.
    /// </summary>
    public int? StartLine { get; init; }

    /// <summary>
    /// Последняя строка, охватываемая узлом, или <see langword="null"/> если неизвестно.
    /// </summary>
    public int? EndLine { get; init; }
}

