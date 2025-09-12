using Rca.Contracts;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Rca.UI.Services
{
    /// <summary>
    /// Factory for creating default service implementations when services are not available.
    /// </summary>
    public static class DefaultServices
    {
        #region Constants

        private const string PythonServiceUnavailableMessage = "Python execution service not available. Please reload the runtime.";

        #endregion

        #region Factory Methods

        /// <summary>
        /// Creates a default Python execution service.
        /// </summary>
        /// <returns>A default implementation of IPythonExecutionService.</returns>
        public static IPythonExecutionService CreatePythonExecutionService()
        {
            return new NullPythonExecutionService();
        }

        /// <summary>
        /// Creates a default debug log service.
        /// </summary>
        /// <returns>A default implementation of IDebugLogService.</returns>
        public static IDebugLogService CreateDebugLogService()
        {
            return new NullDebugLogService();
        }

        /// <summary>
        /// Creates a default Revit context service.
        /// </summary>
        /// <returns>A default implementation of IRevitContext.</returns>
        public static IRevitContext CreateRevitContext()
        {
            return new NullRevitContext();
        }

        #endregion

        #region Default Implementations

        /// <summary>
        /// Default implementation for Python execution when service is not available.
        /// </summary>
        private class NullPythonExecutionService : IPythonExecutionService
        {
            /// <inheritdoc />
            public Task<string> ExecuteAsync(string code)
            {
                return Task.FromResult(PythonServiceUnavailableMessage);
            }

            /// <inheritdoc />
            public string ExecuteSync(string code)
            {
                return PythonServiceUnavailableMessage;
            }

            /// <inheritdoc />
            public Task<string> ExecuteSmartAsync(string code)
            {
                return Task.FromResult(PythonServiceUnavailableMessage);
            }

            /// <inheritdoc />
            public void SetRevitContext(object context)
            {
                // No operation for null implementation
            }
        }
        
        /// <summary>
        /// Default implementation for debug logging when service is not available.
        /// </summary>
        private class NullDebugLogService : IDebugLogService
        {
            private readonly ReadOnlyObservableCollection<IDebugLogEntry> emptyEntries;

            /// <summary>
            /// Initializes a new instance of the NullDebugLogService class.
            /// </summary>
            public NullDebugLogService()
            {
                var emptyCollection = new ObservableCollection<IDebugLogEntry>();
                emptyEntries = new ReadOnlyObservableCollection<IDebugLogEntry>(emptyCollection);
            }

            /// <inheritdoc />
            public ReadOnlyObservableCollection<IDebugLogEntry> Entries => emptyEntries;

            /// <inheritdoc />
            public void LogError(string message)
            {
                if (!string.IsNullOrEmpty(message))
                {
                    Debug.WriteLine($"[ERROR] {message}");
                }
            }

            /// <inheritdoc />
            public void LogInfo(string message)
            {
                if (!string.IsNullOrEmpty(message))
                {
                    Debug.WriteLine($"[INFO] {message}");
                }
            }

            /// <inheritdoc />
            public void LogPythonOutput(string message)
            {
                if (!string.IsNullOrEmpty(message))
                {
                    Debug.WriteLine($"[PYTHON] {message}");
                }
            }

            /// <inheritdoc />
            public void LogCustom(string message, DebugLogType type)
            {
                if (!string.IsNullOrEmpty(message))
                {
                    Debug.WriteLine($"[{type}] {message}");
                }
            }
        }
        
        /// <summary>
        /// Default implementation for Revit context when service is not available.
        /// </summary>
        private class NullRevitContext : IRevitContext
        {
            /// <inheritdoc />
            public object CurrentUIApplication { get; set; }
        }

        #endregion
    }
}