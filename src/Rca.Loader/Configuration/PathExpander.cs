using System;
using System.IO;

namespace Rca.Loader.Configuration
{
    /// <summary>
    /// Utility for expanding environment variables and custom macros in paths.
    /// </summary>
    public static class PathExpander
    {
        /// <summary>
        /// Expands environment variables in a path string.
        /// Supports standard environment variables: %TEMP%, %USERPROFILE%, %PROGRAMDATA%, etc.
        /// </summary>
        /// <param name="path">Path with variables to expand.</param>
        /// <returns>Fully expanded path.</returns>
        public static string ExpandPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            // Expand standard environment variables (%TEMP%, %USERPROFILE%, etc.)
            var expanded = Environment.ExpandEnvironmentVariables(path);

            return Path.GetFullPath(expanded);
        }
    }
}
