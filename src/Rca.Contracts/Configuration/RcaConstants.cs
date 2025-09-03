using System;
using System.IO;

namespace Rca.Contracts.Configuration
{
    /// <summary>
    /// Centralized configuration constants for the RCA plugin.
    /// </summary>
    public static class RcaConstants
    {
        public const string PipeName = "RCA_PIPE";
        public const string RuntimeFileName = "Rca.Runtime.dll";
        public const string RibbonTabName = "RCA";
        public const string ErrorDialogTitle = "RCA";
        public const string RevitVersion = "2026";
        
        public static readonly string RuntimeDeployRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "RCA", 
            "Runtime");
    }
}