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
    /// Named pipe for loader <-> UI commands.
    /// Delegates to generated build metadata single source of truth.
    /// </summary>
    public static string CommandPipeName => RcaBuildMetadata.CommandPipeName;

    /// <summary>
    /// Path to Revit API libraries (RevitAPI.dll, RevitAPIUI.dll).
    /// </summary>
    public static string RevitLibsPath => RcaBuildMetadata.RevitLibsPath;

    /// <summary>
    /// Name of the named pipe for UI logging transport.
    /// Delegates to generated build metadata single source of truth.
    /// </summary>
    public static string LogPipeName => RcaBuildMetadata.LogPipeName;

    /// <summary>
    /// Directory where Revit Addins and timestamp subfolders are located.
    /// Delegates to generated build metadata single source of truth.
    /// </summary>
    public static string RevitAddinsDir => RcaBuildMetadata.RevitAddinsDir;

    /// <summary>
    /// Root directory where integration test builds are deployed.
    /// Delegates to generated build metadata single source of truth.
    /// </summary>
    public static string TestDeployRoot => RcaBuildMetadata.TestDeployRoot;

    /// <summary>
    /// Root directory where RCA logs are written.
    /// Delegates to generated build metadata single source of truth.
    /// </summary>
    public static string LogRoot => RcaBuildMetadata.LogRoot;

    /// <summary>
    /// Revit version used for deployment paths.
    /// </summary>
    public static string RevitVersion => RcaBuildMetadata.RevitVersion;

    /// <summary>
    /// Directory of the currently loaded Loader assembly. Points to the deployment folder (timestamped).
    /// </summary>
    public static readonly string RcaLoaderDir =
        Path.GetDirectoryName(typeof(LoaderConstants).Assembly.Location) ?? RevitAddinsDir;

    /// <summary>
    /// The full path to the deployed Loader assembly.
    /// </summary>
    public static readonly string LoaderAssemblyPath =
        typeof(LoaderConstants).Assembly.Location;

    /// <summary>
    /// Unified manifest of assemblies that compose the Loader group (Single Source of Truth from MSBuild).
    /// </summary>
    public static readonly string[] LoaderAssemblies =
        Array.ConvertAll(RcaBuildMetadata.LoaderProjects, p => $"{p}.dll");

    /// <summary>
    /// Unified manifest of assemblies that compose the Runtime group (Single Source of Truth from MSBuild).
    /// </summary>
    public static readonly string[] RuntimeAssemblies =
        Array.ConvertAll(RcaBuildMetadata.RuntimeProjects, p => $"{p}.dll");

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
