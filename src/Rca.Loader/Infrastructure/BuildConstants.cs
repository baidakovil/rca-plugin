namespace Rca.Loader.Infrastructure
{
  /// <summary>
  /// Constants used during build process for assembly metadata and hot-reload system.
  /// These values must match those used in MSBuild targets and AttributeInjector tool.
  /// </summary>
  public static class BuildConstants
  {
    /// <summary>
    /// Assembly metadata key for source hash.
    /// Used to identify the version of source code that was compiled into the assembly.
    /// </summary>
    public const string SourceHashMetadataKey = "SourceHash";

    /// <summary>
    /// Assembly metadata key for deploy folder timestamp.
    /// Used to correlate deployed assemblies with their timestamped deploy directories.
    /// </summary>
    public const string DeployFolderMetadataKey = "DeployFolder";
  }
}
