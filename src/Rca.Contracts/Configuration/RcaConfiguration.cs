using System;
using System.IO;
// CommandPipeName is centralized in build metadata and used in loader/provider layers, not needed here.

namespace Rca.Contracts.Configuration
{
    /// <summary>
    /// Centralized configuration constants for the RCA plugin.
    /// </summary>
    public static class RcaConfiguration
    {
        /// <summary>
        /// Error dialog title prefix.
        /// </summary>
        public const string ErrorDialogTitle = "RCA";
    }
}