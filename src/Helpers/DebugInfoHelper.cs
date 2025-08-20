using RcaPlugin.Services;

namespace RcaPlugin.Helpers
{
    /// <summary>
    /// Helper for writing custom debug messages to the debug info window.
    /// </summary>
    public static class DebugInfoHelper
    {
        public static void Write(string message)
        {
            DebugLogService.LogInfo(message);
        }

        public static void WriteError(string message)
        {
            DebugLogService.LogError(message);
        }

        public static void WritePython(string message)
        {
            DebugLogService.LogPythonOutput(message);
        }
    }
}
