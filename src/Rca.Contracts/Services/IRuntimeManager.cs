using System.Threading.Tasks;

namespace Rca.Contracts.Services
{
  /// <summary>
  /// Service for managing runtime loading and unloading operations.
  /// </summary>
  public interface IRuntimeManager
  {
    /// <summary>
    /// Loads a runtime from the specified folder path.
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> LoadRuntimeAsync(string folderPath);

    /// <summary>
    /// Reloads the latest deployed runtime.
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> ReloadLatestAsync();

    /// <summary>
    /// Unloads the current runtime.
    /// </summary>
    void UnloadRuntime();

    /// <summary>
    /// Shows the standalone window if runtime is loaded.
    /// </summary>
    (bool Success, string? ErrorMessage) ShowStandaloneWindow();

    /// <summary>
    /// Indicates whether a runtime is currently loaded.
    /// </summary>
    bool IsRuntimeLoaded { get; }
  }
}
