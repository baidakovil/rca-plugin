using Rca.Core.Services;
using System.Collections.ObjectModel;

namespace Rca.UI.ViewModels
{
    /// <summary>
    /// ViewModel for the DebugInfoWindow, exposes log entries.
    /// </summary>
    public class DebugInfoViewModel
    {
        public ReadOnlyObservableCollection<DebugLogEntry> LogEntries => DebugLogService.Entries;
    }
}
