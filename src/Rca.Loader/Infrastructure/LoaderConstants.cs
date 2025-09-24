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
        /// The base directory where Revit loads addins from.
        /// </summary>
        public static readonly string RevitAddinDir = 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "Autodesk", "Revit", "Addins", "2026");
                
        /// <summary>
        /// The directory for the Rca addin where the merged Loader assembly is deployed.
        /// </summary>
        public static readonly string RcaAddinDir =
            Path.Combine(RevitAddinDir, "Rca");
            
        /// <summary>
        /// The full path to the deployed merged Loader assembly.
        /// </summary>
        public static readonly string LoaderAssemblyPath =
            Path.Combine(RcaAddinDir, LoaderFileName);
                
        /// <summary>
        /// The path to the JSON file that stores information about loaded assemblies.
        /// </summary>
        public static readonly string LoadedAssembliesJsonPath = 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                "RCA", "LoadedAssemblies.json");
    }
}
