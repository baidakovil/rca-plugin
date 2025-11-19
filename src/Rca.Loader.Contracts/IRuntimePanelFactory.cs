using System.Windows;

namespace Rca.Loader.Contracts
{
  /// <summary>
  /// Factory provided by the runtime to create the dockable panel content.
  /// Implemented by the runtime and discovered by the Loader to obtain the real UI element.
  /// </summary>
  public interface IRuntimePanelFactory
  {
    /// <summary>
    /// Creates the dockable panel FrameworkElement to host inside the Loader's placeholder.
    /// Implementations should not assume they are called on any particular thread; the Loader will marshal to the UI thread as needed.
    /// </summary>
    /// <returns>A FrameworkElement instance representing the runtime UI, or null on failure.</returns>
    FrameworkElement? CreatePanel();
  }
}
