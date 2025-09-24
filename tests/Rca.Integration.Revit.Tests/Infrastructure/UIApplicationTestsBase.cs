using Autodesk.Revit.UI;

namespace Rca.Integration.Revit.Tests.Infrastructure
{
    /// <summary>
    /// Base class for tests that require Revit UI Application context.
    /// This replaces the dependency on Rca.Loader.Testing.UIApplicationTests to avoid circular dependencies.
    /// </summary>
    public abstract class UIApplicationTestsBase
    {
        /// <summary>
        /// Gets the Revit UI application instance, set by the test adapter.
        /// </summary>
        protected UIApplication? uiapp;

        /// <summary>
        /// Called by the test adapter to set up the Revit context.
        /// </summary>
        /// <param name="uiapp">The Revit UI application instance.</param>
        public virtual void GlobalSetup(UIApplication uiapp)
        {
            this.uiapp = uiapp;
        }
    }
}
