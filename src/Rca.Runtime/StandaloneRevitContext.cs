using System;
using System.Diagnostics;
using Rca.Contracts;

namespace Rca.Runtime
{
    /// <summary>
    /// A standalone implementation of IRevitContext for use when running outside of Revit.
    /// </summary>
    public class StandaloneRevitContext : IRevitContext
    {
        /// <summary>
        /// Gets or sets the current UI application.
        /// In standalone mode, this will always be null.
        /// </summary>
        public object CurrentUIApplication
        {
            get
            {
                Debug.WriteLine("StandaloneRevitContext: Accessing CurrentUIApplication (null in standalone mode)");
                return null;
            }
            set
            {
                Debug.WriteLine("StandaloneRevitContext: Setting CurrentUIApplication (ignored in standalone mode)");
                // Intentionally ignored in standalone mode
            }
        }
    }
}