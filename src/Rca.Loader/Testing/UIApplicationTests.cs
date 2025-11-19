using Autodesk.Revit.UI;

namespace Rca.Loader.Testing
{
  /// <summary>
  /// Base class for tests that require a UIApplication instance.
  /// </summary>
  public abstract class UIApplicationTests
  {
    /// <summary>
    /// Gets or sets the Revit UI application.
    /// </summary>
    protected UIApplication? uiapp;

    /// <summary>
    /// Sets up the test with the Revit UI application.
    /// </summary>
    /// <param name="uiapp">The Revit UI application.</param>
    public void GlobalSetup(UIApplication uiapp)
    {
      this.uiapp = uiapp;
    }
  }
}
