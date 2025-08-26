namespace Rca.Contracts
{
    /// <summary>
    /// Interface for Revit context abstraction.
    /// </summary>
    public interface IRevitContext
    {
        /// <summary>
        /// Gets the current UI application.
        /// </summary>
        object CurrentUIApplication { get; set; }
    }
}