using System;

namespace RcaPlugin.Services
{
    /// <summary>
    /// Represents a single debug log entry.
    /// </summary>
    public class DebugLogEntry
    {
        public DateTime Timestamp { get; set; }
        public DebugLogType Type { get; set; }
        public string Message { get; set; }
    }

    public enum DebugLogType
    {
        Info,
        Error,
        PythonOutput
    }
}
