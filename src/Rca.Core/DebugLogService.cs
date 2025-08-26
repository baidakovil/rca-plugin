using System;
using System.Collections.ObjectModel;

namespace Rca.Core.Services
{
    /// <summary>
    /// Service for collecting and broadcasting debug log entries.
    /// </summary>
    public static class DebugLogService
    {
        private static readonly ObservableCollection<DebugLogEntry> entries = new();
        public static ReadOnlyObservableCollection<DebugLogEntry> Entries { get; } = new(entries);

        public static void LogInfo(string message)
        {
            AddEntry(DebugLogType.Info, message);
        }

        public static void LogError(string message)
        {
            AddEntry(DebugLogType.Error, message);
        }

        public static void LogPythonOutput(string message)
        {
            AddEntry(DebugLogType.PythonOutput, message);
        }

        public static void LogCustom(string message, DebugLogType type)
        {
            AddEntry(type, message);
        }

        private static void AddEntry(DebugLogType type, string message)
        {
            // Pad the type to a fixed width for alignment (e.g., 12 chars)
            string paddedType = type.ToString().PadRight(12);
            entries.Add(new DebugLogEntry
            {
                Timestamp = DateTime.Now,
                Type = type, // keep the original type for binding
                Message = $"[{paddedType}] {message}"
            });
        }
    }
}
