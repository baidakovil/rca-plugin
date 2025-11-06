namespace Rca.Tools.MetricsReporter.Processing;

using System.Collections.Generic;

/// <summary>
/// Представляет результаты парсинга одного источника метрик.
/// </summary>
public sealed class ParsedMetricsDocument
{
    /// <summary>
    /// Имя solution, полученное из источника или аргументов.
    /// </summary>
    public string SolutionName { get; init; } = string.Empty;

    /// <summary>
    /// Элементы кода, обнаруженные в источнике.
    /// </summary>
    public IList<ParsedCodeElement> Elements { get; init; } = new List<ParsedCodeElement>();
}

