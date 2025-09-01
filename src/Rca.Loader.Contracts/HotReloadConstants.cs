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
        /// Default manifest file name.
        /// </summary>
        public const string ManifestFileName = "current.json";
        
        /// <summary>
        /// Default runtime assembly name.
        /// </summary>
        public const string RuntimeAssemblyName = "Rca.Dynamic.dll";
        
        /// <summary>
        /// Default staging directory name under %APPDATA%/RCA.
        /// </summary>
        public const string StagingDirectoryName = "LiveCore";
    }
}