using Autodesk.Revit.UI;
using Rca.Contracts;

namespace Rca.Core
{
    /// <summary>
    /// Implementation of IRevitContext for dependency injection.
    /// </summary>
    internal class RevitContextServiceImpl : IRevitContext
    {
        public object CurrentUIApplication 
        { 
            get => RevitContext.CurrentUIApplication;
            set => RevitContext.CurrentUIApplication = value as UIApplication;
        }
    }

    /// <summary>
    /// Holds the current UIApplication for use by modeless windows and panels.
    /// </summary>
    public static class RevitContext
    {
        private static UIApplication currentUIApplication;

        /// <summary>
        /// Gets or sets the current UI application.
        /// </summary>
        public static UIApplication CurrentUIApplication 
        { 
            get => currentUIApplication;
            set => currentUIApplication = value;
        }

        /// <summary>
        /// Gets the singleton instance for dependency injection.
        /// </summary>
        public static IRevitContext Instance { get; } = new RevitContextServiceImpl();
    }
}
