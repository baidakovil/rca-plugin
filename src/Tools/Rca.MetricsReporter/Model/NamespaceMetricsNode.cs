namespace Rca.Tools.MetricsReporter.Model;

using System.Collections.Generic;

/// <summary>
/// Узел уровня пространства имён.
/// </summary>
public sealed class NamespaceMetricsNode : MetricsNode
{
    /// <summary>
    /// Инициализирует узел пространства имён.
    /// </summary>
    public NamespaceMetricsNode()
        => Kind = CodeElementKind.Namespace;

    /// <summary>
    /// Коллекция типов внутри пространства имён.
    /// </summary>
    public IList<TypeMetricsNode> Types { get; init; }
        = new List<TypeMetricsNode>();
}

