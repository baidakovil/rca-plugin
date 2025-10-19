namespace Rca.Loader.Contracts
{
    /// <summary>
    /// Interface for Revit ribbon building service.
    /// </summary>
    public interface IRibbonService
    {
        /// <summary>
        /// Builds the RCA ribbon controls in Revit's Add-Ins tab.
        /// Creates "Intelligence Tools" panel (always visible) with Revit Chat Assistant button.
        /// In DEBUG builds, also creates "RCA Debug" panel with development tools.
        /// </summary>
        /// <param name="application">The Revit UI controlled application.</param>
        void BuildRibbon(object application);
    }
}
