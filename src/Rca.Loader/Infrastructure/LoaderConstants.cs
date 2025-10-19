using System;
using System.IO;

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
        /// The name of the loader DLL file.
        /// </summary>
        public const string LoaderFileName = "Rca.Loader.dll";
        
        /// <summary>
        /// The name of the named pipe for communication.
        /// </summary>
        public const string PipeName = "RCA_PIPE";
        
        /// <summary>
        /// The root directory where runtime versions are deployed.
        /// This is where timestamped runtime folders are created.
        /// </summary>
        public static readonly string RuntimeDeployRoot = 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RCA", "Runtime");

        /// <summary>
        /// The root directory where integration test builds are deployed.
        /// A timestamped subfolder (yyyyMMdd_HHmmss) is created per test build to avoid file locks.
        /// </summary>
        public static readonly string TestDeployRoot =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RCA", "Test");
        
        /// <summary>
        /// The base directory where Revit loads addins from.
        /// </summary>
        public static readonly string RevitAddinDir = 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "Autodesk", "Revit", "Addins", "2026");
                
        /// <summary>
        /// The directory for the Rca addin where the Loader assembly is deployed.
        /// </summary>
        public static readonly string RcaAddinDir =
            Path.Combine(RevitAddinDir, "Rca");
            
        /// <summary>
        /// The full path to the deployed Loader assembly.
        /// </summary>
        public static readonly string LoaderAssemblyPath =
            Path.Combine(RcaAddinDir, LoaderFileName);

        /// <summary>
        /// Unified manifest of assemblies that compose the Loader group.
        /// </summary>
        public static readonly string[] LoaderAssemblies =
        {
            "Rca.Loader.dll",
            "Rca.Loader.Contracts.dll",
            "Rca.Logging.Contracts.dll"
        };

        /// <summary>
        /// Unified manifest of assemblies that compose the Runtime group.
        /// </summary>
        public static readonly string[] RuntimeAssemblies =
        {
            "Rca.Runtime.dll",
            "Rca.Core.dll",
            "Rca.Network.dll",
            "Rca.UI.dll",
            "Rca.Contracts.dll"
        };
    }
}
