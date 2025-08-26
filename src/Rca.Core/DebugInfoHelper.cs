using Rca.Core.Services;

namespace Rca.Core.Helpers
{
    /// <summary>
    /// Helper for writing custom debug messages to the debug info window.
    /// </summary>
    public static class DebugInfoHelper
    {
        public static void Write(string message)
        {
            DebugLogService.StaticLogInfo(message);
        }

        public static void WriteError(string message)
        {
            DebugLogService.StaticLogError(message);
        }

        public static void WritePython(string message)
        {
            DebugLogService.StaticLogPythonOutput(message);
        }
    }
}
