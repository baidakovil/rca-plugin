namespace Rca.Tools.MetricsReporter.Model;

using System.Collections.Generic;

/// <summary>
/// Узел уровня типа (class/struct/record и т.д.).
/// </summary>
public sealed class TypeMetricsNode : MetricsNode
{
    /// <summary>
    /// Создаёт узел для типа.
    /// </summary>
    public TypeMetricsNode()
        => Kind = CodeElementKind.Type;

    /// <summary>
    /// Методы, свойства и другие члены типа.
    /// </summary>
    public IList<MemberMetricsNode> Members { get; init; }
        = new List<MemberMetricsNode>();
}

