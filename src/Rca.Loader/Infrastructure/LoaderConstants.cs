using System;
using System.IO;
using System.Reflection;
using Rca.Generated;

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
        /// Directory where Revit Addins and timestamp subfolders are located.
        /// Delegates to generated build metadata single source of truth.
        /// </summary>
        public static string RevitAddinsDir => RcaBuildMetadata.RevitAddinsDir;
        
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
        /// The directory of the currently loaded Loader assembly. This resolves to the
        /// timestamped deployment folder (e.g., Addins/2026/20250101_120000).
        /// </summary>
        public static readonly string RcaAddinDir =
            Path.GetDirectoryName(typeof(LoaderConstants).Assembly.Location) ?? RevitAddinDir;
            
        /// <summary>
        /// The full path to the deployed Loader assembly.
        /// </summary>
        public static readonly string LoaderAssemblyPath =
            typeof(LoaderConstants).Assembly.Location;

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

        /// <summary>
        /// Length of the short source hash used for Loader/Runtime groups.
        /// </summary>
        public static int SourceHashLength => RcaBuildMetadata.SourceHashLength;

        /// <summary>
        /// Timestamp pattern for build output directory names.
        /// </summary>
        public static string TimestampPattern => RcaBuildMetadata.TimestampPattern;
    }
}
