namespace Rca.Loader.Contracts
{
    /// <summary>
    /// Constants for the hot reload system.
    /// </summary>
    public static class HotReloadConstants
    {
        /// <summary>
        /// Name of the named pipe for hot reload communication.
        /// </summary>
        public const string PipeName = "rca.hotreload";

        /// <summary>
        /// Timeout for pipe operations in milliseconds.
        /// </summary>
        public const int PipeTimeoutMs = 5000;
    }
}