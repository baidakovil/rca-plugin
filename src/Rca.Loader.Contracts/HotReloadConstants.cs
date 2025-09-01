namespace Rca.Loader.Contracts
{
    /// <summary>
    /// Constants for the hot reload system.
    /// </summary>
    public static class HotReloadConstants
    {
        /// <summary>
        /// Named pipe name for hot reload communication.
        /// </summary>
        public const string PipeName = "rca.hotreload";

        /// <summary>
        /// Default staging directory for hot reload assemblies.
        /// </summary>
        public const string StagingDirectory = "RCA\\LiveCore";

        /// <summary>
        /// Name of the current runtime manifest file.
        /// </summary>
        public const string ManifestFileName = "current.json";

        /// <summary>
        /// Name of the merged runtime assembly.
        /// </summary>
        public const string RuntimeAssemblyName = "Rca.Dynamic.dll";
    }
}