namespace Rca.Loader.Contracts
{
    /// <summary>
    /// Interface for Revit ribbon building service.
    /// </summary>
    public interface IRibbonService
    {
        /// <summary>
        /// Builds the RCA ribbon tab and panels in Revit.
        /// </summary>
        /// <param name="application">The Revit UI controlled application.</param>
        void BuildRibbon(object application);
    }
}