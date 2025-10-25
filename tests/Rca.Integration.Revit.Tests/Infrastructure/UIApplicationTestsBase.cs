using Autodesk.Revit.UI;
using System.IO;

namespace Rca.Integration.Revit.Tests.Infrastructure
{
    /// <summary>
    /// Base class for tests that require Revit UI Application context.
    /// This replaces the dependency on Rca.Loader.Testing.UIApplicationTests to avoid circular dependencies.
    /// </summary>
    public abstract class UIApplicationTestsBase
    {
        private static bool isInitialized;
        protected static TestLogger? TestLoggerInstance;

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

            // Initialize test logging once per test assembly execution. We do it here instead of
            // OneTimeSetUp because the Revit test adapter guarantees this hook is invoked.
            if (!isInitialized)
            {
                try
                {
                    var dir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "RCA", "Logs");
                    Directory.CreateDirectory(dir);

                    if (TestLoggerInstance == null) TestLoggerInstance = TestLogger.Start();
                    TestLoggerInstance.Log("UIApplicationTestsBase.GlobalSetup: logger initialized");
                }
                catch
                {
                    // Ignore initialization errors to avoid breaking tests
                }
                finally
                {
                    isInitialized = true;
                }
            }
        }
    }
}
