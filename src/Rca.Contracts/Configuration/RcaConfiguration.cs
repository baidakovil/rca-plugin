using System;
using System.IO;

namespace Rca.Contracts.Configuration
{
    /// <summary>
    /// Centralized configuration constants for the RCA plugin.
    /// </summary>
    public static class RcaConfiguration
    {
        /// <summary>
        /// Named pipe name for hot-reload communication.
        /// </summary>
        public const string PipeName = "RCA_PIPE";

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
        /// Revit version targeted by the plugin.
        /// </summary>
        public const string RevitVersion = "2026";

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