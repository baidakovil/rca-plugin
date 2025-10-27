namespace Rca.TestAdapter;

/// <summary>
/// Constants used by the test adapter.
/// </summary>
internal static class Constants
{
    /// <summary>
    /// The unique identifier for the test executor.
    /// </summary>
    public const string ExecutorUri = "executor://RcaRevitTestExecutor";
    
    /// <summary>
    /// The named pipe used for communication with Revit commands.
    /// Delegates to centralized build metadata.
    /// </summary>
    public const string CommandPipeName = "RCA_COMMAND_PIPE";
}