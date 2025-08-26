namespace Rca.Contracts
{
    /// <summary>
    /// Interface for Revit context abstraction.
    /// </summary>
    public interface IRevitContext
    {
        /// <summary>
        /// Gets or sets the current UI application.
        /// </summary>
        object CurrentUIApplication { get; set; }
    }
}