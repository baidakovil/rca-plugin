using System;

namespace Rca.Loader
{
    /// <summary>
    /// Constants used throughout the loader application.
    /// </summary>
    public static class LoaderConstants
    {
        /// <summary>
        /// Named pipe name used for IPC communication.
        /// </summary>
        public const string PipeName = "RCA_PIPE";
        
        /// <summary>
        /// Filename of the runtime assembly.
        /// </summary>
        public const string RuntimeFileName = "Rca.Runtime.dll";
        
        /// <summary>
        /// Root directory where runtime versions are deployed.
        /// </summary>
        public static readonly string RuntimeDeployRoot = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "RCA", 
            "Runtime");
    }
}