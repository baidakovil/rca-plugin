using System.Windows;

namespace Rca.Loader.Contracts
{
    /// <summary>
    /// Host interface for the dockable panel placeholder provided by the Loader.
    /// The Loader registers a dockable pane containing a control implementing this interface.
    /// The Runtime can later provide a <see cref="FrameworkElement"/> to replace the placeholder.
    /// </summary>
    public interface IRuntimePanelHost
    {
        /// <summary>
        /// Replaces the current content of the host with the provided WPF element.
        /// Passing null will clear the content and return the host to placeholder state.
        /// This method must be safe to call from the Revit UI thread.
        /// </summary>
        /// <param name="content">The framework element to set as content, or null to clear.</param>
        void SetContent(FrameworkElement? content);

        /// <summary>
        /// Gets the currently set content element, if any.
        /// </summary>
        /// <returns>The currently set FrameworkElement or null if none.</returns>
        FrameworkElement? GetContent();
    }
}
