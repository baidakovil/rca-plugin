namespace Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Represents a member-level node.
/// </summary>
public sealed class MemberMetricsNode : MetricsNode
{
    /// <summary>
    /// Initialises a member node.
    /// </summary>
    public MemberMetricsNode()
        => Kind = CodeElementKind.Member;
}

