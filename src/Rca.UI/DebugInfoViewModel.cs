using Rca.Contracts;
using System.Collections.ObjectModel;

namespace Rca.UI.ViewModels
{
    /// <summary>
    /// ViewModel for the DebugInfoWindow, exposes log entries.
    /// </summary>
    public class DebugInfoViewModel
    {
        private readonly IDebugLogService debugLogService;

        /// <summary>
        /// Initializes a new instance of the DebugInfoViewModel class.
        /// </summary>
        /// <param name="debugLogService">The debug log service</param>
        public DebugInfoViewModel(IDebugLogService debugLogService)
        {
            this.debugLogService = debugLogService;
        }

        /// <summary>
        /// Gets the log entries from the debug log service.
        /// </summary>
        public ReadOnlyObservableCollection<IDebugLogEntry> LogEntries => debugLogService.Entries;
    }
}
