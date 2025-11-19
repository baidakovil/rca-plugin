using System;
using System.Threading.Tasks;

namespace Rca.Contracts.Services
{
  /// <summary>
  /// Service for managing runtime loading and unloading.
  /// </summary>
  public interface IRuntimeManagerService
  {
    /// <summary>
    /// Loads a runtime from the specified folder path.
    /// </summary>
    /// <param name="folderPath">Path to the runtime folder.</param>
    /// <returns>True if successful, false otherwise.</returns>
    Task<(bool Success, string? ErrorMessage)> LoadRuntimeAsync(string folderPath);

    /// <summary>
    /// Reloads the latest deployed runtime.
    /// </summary>
    /// <returns>True if successful, false otherwise.</returns>
    Task<(bool Success, string? ErrorMessage)> ReloadLatestRuntimeAsync();

    /// <summary>
    /// Unloads the current runtime.
    /// </summary>
    void UnloadRuntime();

    /// <summary>
    /// Shows the standalone window if runtime is loaded.
    /// </summary>
    /// <returns>True if successful, false otherwise.</returns>
    (bool Success, string? ErrorMessage) ShowStandaloneWindow();

    /// <summary>
    /// Indicates whether a runtime is currently loaded.
    /// </summary>
    bool IsRuntimeLoaded { get; }
  }
}
