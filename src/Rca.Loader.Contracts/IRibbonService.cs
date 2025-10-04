namespace Rca.Loader.Contracts
{
    /// <summary>
    /// Interface for Revit ribbon building service.
    /// </summary>
    public interface IRibbonService
    {
        /// <summary>
        /// Builds the RCA ribbon controls in Revit's Add-Ins tab.
        /// In DEBUG builds, creates controls for development and testing.
        /// In RELEASE builds, no UI is created.
        /// </summary>
        /// <param name="application">The Revit UI controlled application.</param>
        void BuildRibbon(object application);
    }
}
