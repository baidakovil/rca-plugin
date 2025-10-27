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
        /// Runtime assembly file name.
        /// </summary>
        public const string RuntimeFileName = "Rca.Runtime.dll";

        /// <summary>
        /// Root directory for runtime deployments.
        /// </summary>
        public static readonly string RuntimeDeployRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "RCA", 
            "Runtime");

        /// <summary>
        /// RCA ribbon tab name.
        /// </summary>
        public const string RibbonTabName = "RCA";

        /// <summary>
        /// Error dialog title prefix.
        /// </summary>
        public const string ErrorDialogTitle = "RCA";
    }
}