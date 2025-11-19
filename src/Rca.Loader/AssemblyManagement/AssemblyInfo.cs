using System;

namespace Rca.Loader.AssemblyManagement
{
  /// <summary>
  /// Represents information about a loaded assembly, including its path and hash.
  /// </summary>
  /// <remarks>
  /// This class is used as part of the hot-reload system to track assembly changes.
  /// </remarks>
  public class AssemblyInfo
  {
    /// <summary>
    /// Gets or sets the full file path to the assembly.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SHA256 hash of the assembly file content.
    /// </summary>
    /// <remarks>The hash is used to detect changes in the assembly between builds.</remarks>
    public string Hash { get; set; } = string.Empty;
  }
}
