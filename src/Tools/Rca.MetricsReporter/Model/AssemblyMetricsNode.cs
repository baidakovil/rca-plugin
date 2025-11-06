namespace Rca.Tools.MetricsReporter.Model;

using System.Collections.Generic;

/// <summary>
/// Узел уровня сборки (MSBuild-проекта).
/// </summary>
public sealed class AssemblyMetricsNode : MetricsNode
{
    /// <summary>
    /// Инициализирует новый экземпляр узла сборки.
    /// </summary>
    public AssemblyMetricsNode()
        => Kind = CodeElementKind.Assembly;

    /// <summary>
    /// Дочерние пространства имён.
    /// </summary>
    public IList<NamespaceMetricsNode> Namespaces { get; init; }
        = new List<NamespaceMetricsNode>();
}

