using System;
using Rca.Contracts;

namespace Rca.Core.Services
{
    /// <summary>
    /// Represents a single debug log entry.
    /// </summary>
    public class DebugLogEntry : IDebugLogEntry
    {
        public DateTime Timestamp { get; set; }
        public DebugLogType Type { get; set; }
        public string Message { get; set; }
    }
}
