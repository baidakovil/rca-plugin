namespace Rca.Tools.MetricsReporter.Model;

using System.Collections.Generic;

/// <summary>
/// Базовый узел иерархии метрик.
/// </summary>
public abstract class MetricsNode
{
    /// <summary>
    /// Имя узла (как отображается в UI).
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Тип узла (solution/assembly/namespace/type/member).
    /// </summary>
    public CodeElementKind Kind { get; init; }
        = CodeElementKind.Member;

    /// <summary>
    /// Полностью квалифицированное имя (FQN). Для solution/namespace может быть <see langword="null"/>.
    /// </summary>
    public string? FullyQualifiedName { get; init; }
        = null;

    /// <summary>
    /// Исходное расположение в файле, используется для сопоставления SARIF и подсказок в HTML.
    /// </summary>
    public SourceLocation? Source { get; set; }
        = null;

    /// <summary>
    /// Признак того, что узел отсутствовал в baseline.
    /// </summary>
    public bool IsNew { get; set; }
        = false;

    /// <summary>
    /// Набор значений метрик для текущего узла.
    /// </summary>
    public IDictionary<MetricIdentifier, MetricValue> Metrics { get; set; }
        = new Dictionary<MetricIdentifier, MetricValue>();
}

