namespace Rca.Tools.MetricsReporter.Processing;

using System.Collections.Generic;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Представляет элемент кода, полученный из исходного источника метрик.
/// </summary>
public sealed class ParsedCodeElement
{
    /// <summary>
    /// Создаёт экземпляр <see cref="ParsedCodeElement"/>.
    /// </summary>
    /// <param name="kind">Уровень иерархии.</param>
    /// <param name="name">Отображаемое имя элемента.</param>
    /// <param name="fullyQualifiedName">Полностью квалифицированное имя или <see langword="null"/>.</param>
    public ParsedCodeElement(CodeElementKind kind, string name, string? fullyQualifiedName)
    {
        Kind = kind;
        Name = name;
        FullyQualifiedName = fullyQualifiedName;
    }

    /// <summary>
    /// Уровень иерархии (assembly/type/member и т.д.).
    /// </summary>
    public CodeElementKind Kind { get; }

    /// <summary>
    /// Отображаемое имя элемента.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Полностью квалифицированное имя или <see langword="null"/>.
    /// </summary>
    public string? FullyQualifiedName { get; }

    /// <summary>
    /// Полностью квалифицированное имя родительского элемента.
    /// </summary>
    public string? ParentFullyQualifiedName { get; init; }

    /// <summary>
    /// Локальное расположение в исходном файле (если известно).
    /// </summary>
    public SourceLocation? Source { get; init; }

    /// <summary>
    /// Метрики, предоставленные данным источником.
    /// </summary>
    public IDictionary<MetricIdentifier, MetricValue> Metrics { get; init; } = new Dictionary<MetricIdentifier, MetricValue>();
}

