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
    /// The name of the named pipe used for communication with Revit.
    /// </summary>
    public const string PipeName = "RCA_PIPE";
}