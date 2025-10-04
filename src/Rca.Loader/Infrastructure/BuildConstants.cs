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

        /// <summary>
        /// File name pattern for loader source hash files in deploy directory.
        /// Format: SourceHash-Loader-{hash}.txt
        /// </summary>
        public const string LoaderHashFilePattern = "SourceHash-Loader-*.txt";

        /// <summary>
        /// File name pattern for runtime source hash files in deploy directory.
        /// Format: SourceHash-Runtime-{hash}.txt
        /// </summary>
        public const string RuntimeHashFilePattern = "SourceHash-Runtime-*.txt";

        /// <summary>
        /// Intermediate file name for loader source hash during build.
        /// </summary>
        public const string LoaderHashIntermediateFile = "source-hash-loader.txt";

        /// <summary>
        /// Intermediate file name for runtime source hash during build.
        /// </summary>
        public const string RuntimeHashIntermediateFile = "source-hash-runtime.txt";
    }
}
