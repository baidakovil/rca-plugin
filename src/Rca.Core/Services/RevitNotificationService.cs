#nullable enable

using Autodesk.Revit.UI;
using Rca.Contracts.Configuration;
using Rca.Contracts.Services;

namespace Rca.Core.Services
{
    /// <summary>
    /// Implementation of INotificationService using Revit TaskDialog.
    /// </summary>
    public class RevitNotificationService : INotificationService
    {
        /// <inheritdoc />
        public void ShowError(string message, string? title = null)
        {
            TaskDialog.Show(title ?? $"{RcaConfiguration.ErrorDialogTitle} Error", message);
        }

        /// <inheritdoc />
        public void ShowInfo(string message, string? title = null)
        {
            TaskDialog.Show(title ?? RcaConfiguration.ErrorDialogTitle, message);
        }

        /// <inheritdoc />
        public void ShowWarning(string message, string? title = null)
        {
            TaskDialog.Show(title ?? $"{RcaConfiguration.ErrorDialogTitle} Warning", message);
        }
    }
}