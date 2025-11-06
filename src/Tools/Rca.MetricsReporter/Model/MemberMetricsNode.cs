namespace Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Узел уровня члена типа.
/// </summary>
public sealed class MemberMetricsNode : MetricsNode
{
    /// <summary>
    /// Создаёт узел члена типа.
    /// </summary>
    public MemberMetricsNode()
        => Kind = CodeElementKind.Member;
}

