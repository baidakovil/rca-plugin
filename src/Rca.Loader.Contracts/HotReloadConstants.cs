namespace Rca.Loader.Contracts
{
    /// <summary>
    /// Constants for hot reload functionality.
    /// </summary>
    public static class HotReloadConstants
    {
        /// <summary>
        /// Name of the named pipe for hot reload communication.
        /// </summary>
        public const string PipeName = "rca.hotreload";

        /// <summary>
        /// Default staging root directory for runtime assemblies.
        /// </summary>
        public const string DefaultStagingRoot = "RCA\\LiveCore";

        /// <summary>
        /// Name of the current manifest file.
        /// </summary>
        public const string ManifestFileName = "current.json";

        /// <summary>
        /// Name of the dynamic runtime assembly.
        /// </summary>
        public const string RuntimeAssemblyName = "Rca.Dynamic.dll";
    }
}