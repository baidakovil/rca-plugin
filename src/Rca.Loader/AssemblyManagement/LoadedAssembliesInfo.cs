using System;

namespace Rca.Loader.AssemblyManagement
{
  /// <summary>
  /// Contains information about all assemblies tracked by the hot-reload system.
  /// </summary>
  /// <remarks>
  /// This class serves as the data model for the LoadedAssemblies.json file
  /// that persists assembly state between Revit sessions.
  /// Loader and Contracts are treated as a single unit since they
  /// are always deployed and updated together
  /// </remarks>
  public class LoadedAssembliesInfo
  {
    /// <summary>
    /// Gets or sets information about the loader components (Rca.Loader.dll and Rca.Loader.Contracts.dll).
    /// </summary>
    /// <remarks>
    /// Loader and Contracts are treated as a single component since they are always deployed together.
    /// The path represents the directory containing both assemblies.
    /// The hash is a combined hash of both assemblies.
    /// </remarks>
    public AssemblyInfo LoaderComponents { get; set; } = new AssemblyInfo();

    /// <summary>
    /// Gets or sets information about the Rca.Runtime.dll assembly.
    /// This represents the DISCOVERED version (what's on disk), not necessarily what's loaded.
    /// </summary>
    public AssemblyInfo RuntimeAssembly { get; set; } = new AssemblyInfo();

    /// <summary>
    /// Gets or sets information about the ACTUALLY LOADED runtime assembly.
    /// This is updated only after successful ReloadRuntime operation.
    /// Used to compare with RuntimeAssembly to determine if reload is needed.
    /// </summary>
    public AssemblyInfo LoadedRuntimeAssembly { get; set; } = new AssemblyInfo();

    /// <summary>
    /// Gets or sets information about the last MSBuild signal received.
    /// </summary>
    public SignalInfo LastMSBuildSignal { get; set; } = new SignalInfo();
  }
}
