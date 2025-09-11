using System;

namespace Rca.Loader.Infrastructure
{
    /// <summary>
    /// Constants used throughout the RCA Loader.
    /// </summary>
    public static class LoaderConstants
    {
        /// <summary>
        /// The name of the runtime DLL file.
        /// </summary>
        public const string RuntimeFileName = "Rca.Runtime.dll";
        
        /// <summary>
        /// The name of the named pipe for communication.
        /// </summary>
        public const string PipeName = "RCA_PIPE";
        
        /// <summary>
        /// The root directory where runtime versions are deployed.
        /// </summary>
        public static readonly string RuntimeDeployRoot = 
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RCA", "Runtime");
    }
}