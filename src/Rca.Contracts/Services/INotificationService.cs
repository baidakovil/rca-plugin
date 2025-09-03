namespace Rca.Contracts.Services
{
    /// <summary>
    /// Service for displaying notifications and dialogs to the user.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Shows an error dialog to the user.
        /// </summary>
        /// <param name="message">The error message to display.</param>
        /// <param name="title">Optional title for the dialog.</param>
        void ShowError(string message, string? title = null);

        /// <summary>
        /// Shows an information dialog to the user.
        /// </summary>
        /// <param name="message">The information message to display.</param>
        /// <param name="title">Optional title for the dialog.</param>
        void ShowInfo(string message, string? title = null);

        /// <summary>
        /// Shows a warning dialog to the user.
        /// </summary>
        /// <param name="message">The warning message to display.</param>
        /// <param name="title">Optional title for the dialog.</param>
        void ShowWarning(string message, string? title = null);
    }
}