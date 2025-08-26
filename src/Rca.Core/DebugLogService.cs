using System;
using System.Collections.ObjectModel;
using Rca.Contracts;

namespace Rca.Core.Services
{
    /// <summary>
    /// Service for collecting and broadcasting debug log entries.
    /// </summary>
    public class DebugLogService : IDebugLogService
    {
        private static readonly DebugLogService instance = new();
        private readonly ObservableCollection<DebugLogEntry> entries = new();
        private readonly ReadOnlyObservableCollection<IDebugLogEntry> readOnlyEntries;

        /// <summary>
        /// Gets the singleton instance of the debug log service.
        /// </summary>
        public static DebugLogService Instance => instance;

        /// <summary>
        /// Gets the read-only collection of debug log entries.
        /// </summary>
        public ReadOnlyObservableCollection<IDebugLogEntry> Entries => readOnlyEntries;

        private DebugLogService()
        {
            // Create a read-only wrapper that converts DebugLogEntry to IDebugLogEntry
            var observableEntries = new ObservableCollection<IDebugLogEntry>();
            readOnlyEntries = new ReadOnlyObservableCollection<IDebugLogEntry>(observableEntries);
            
            // When entries change, update the interface collection
            entries.CollectionChanged += (sender, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (DebugLogEntry item in e.NewItems)
                    {
                        observableEntries.Add(item);
                    }
                }
                if (e.OldItems != null)
                {
                    foreach (DebugLogEntry item in e.OldItems)
                    {
                        observableEntries.Remove(item);
                    }
                }
            };
        }

        public void LogInfo(string message)
        {
            AddEntry(DebugLogType.Info, message);
        }

        public void LogError(string message)
        {
            AddEntry(DebugLogType.Error, message);
        }

        public void LogPythonOutput(string message)
        {
            AddEntry(DebugLogType.PythonOutput, message);
        }

        public void LogCustom(string message, DebugLogType type)
        {
            AddEntry(type, message);
        }

        private void AddEntry(DebugLogType type, string message)
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

        #region Static methods for backward compatibility
        public static void LogInfo(string message) => Instance.LogInfo(message);
        public static void LogError(string message) => Instance.LogError(message);
        public static void LogPythonOutput(string message) => Instance.LogPythonOutput(message);
        public static void LogCustom(string message, DebugLogType type) => Instance.LogCustom(message, type);
        public static ReadOnlyObservableCollection<DebugLogEntry> Entries => 
            new(Instance.entries);
        #endregion
    }
}
