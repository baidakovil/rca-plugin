namespace Rca.Contracts
{
  /// <summary>
  /// Describes whether the required external Python runtime is available for python execution.
  /// </summary>
  public sealed class PythonRuntimeStatus
  {
    public const string SupportedVersion = "3.11";
    public const string OfficialDownloadUrl = "https://www.python.org/downloads/windows/";

    private PythonRuntimeStatus(bool isAvailable, string message, string? runtimePath, string? downloadUrl)
    {
      IsAvailable = isAvailable;
      Message = message;
      RuntimePath = runtimePath;
      DownloadUrl = downloadUrl;
    }

    /// <summary>
    /// Gets whether the required Python runtime is available.
    /// </summary>
    public bool IsAvailable { get; }

    /// <summary>
    /// Gets the user-facing status message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the resolved runtime path when Python is available.
    /// </summary>
    public string? RuntimePath { get; }

    /// <summary>
    /// Gets the official download URL when the user should install Python.
    /// </summary>
    public string? DownloadUrl { get; }

    public static PythonRuntimeStatus Available(string runtimePath)
    {
      return new PythonRuntimeStatus(
          isAvailable: true,
          message: $"Python {SupportedVersion} detected: {runtimePath}",
          runtimePath: runtimePath,
          downloadUrl: null);
    }

    public static PythonRuntimeStatus MissingInstallation(string message)
    {
      return new PythonRuntimeStatus(
          isAvailable: false,
          message: message,
          runtimePath: null,
          downloadUrl: OfficialDownloadUrl);
    }

    public static PythonRuntimeStatus Unavailable(string message)
    {
      return new PythonRuntimeStatus(
          isAvailable: false,
          message: message,
          runtimePath: null,
          downloadUrl: null);
    }
  }
}