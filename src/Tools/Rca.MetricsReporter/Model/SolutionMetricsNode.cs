namespace Rca.Tools.MetricsReporter.Model;

using System.Collections.Generic;

/// <summary>
/// Корневой узел с агрегированными данными по всему solution.
/// </summary>
public sealed class SolutionMetricsNode : MetricsNode
{
    /// <summary>
    /// Создаёт корневой узел solution.
    /// </summary>
    public SolutionMetricsNode()
        => Kind = CodeElementKind.Solution;

    /// <summary>
    /// Сборки, включённые в отчёт.
    /// </summary>
    public IList<AssemblyMetricsNode> Assemblies { get; init; }
        = new List<AssemblyMetricsNode>();
}

