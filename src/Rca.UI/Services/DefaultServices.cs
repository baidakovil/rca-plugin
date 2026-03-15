#nullable enable
using Rca.Contracts;
using System.Threading.Tasks;

namespace Rca.UI.Services
{
  /// <summary>
  /// Factory for creating default service implementations when services are not available.
  /// Debug logging service removed.
  /// </summary>
  public static class DefaultServices
  {
    private const string PythonServiceUnavailableMessage = "Python execution service not available. Please reload the runtime.";

    public static IPythonExecutionService CreatePythonExecutionService() => new NullPythonExecutionService();
    public static IRevitContext CreateRevitContext() => new NullRevitContext();

    private class NullPythonExecutionService : IPythonExecutionService
    {
      public PythonRuntimeStatus GetRuntimeStatus() => PythonRuntimeStatus.Unavailable(PythonServiceUnavailableMessage);
      public Task<string> ExecuteAsync(string code) => Task.FromResult(PythonServiceUnavailableMessage);
      public string ExecuteSync(string code) => PythonServiceUnavailableMessage;
      public void SetRevitContext(object context) { }
    }

    private class NullRevitContext : IRevitContext
    {
      public object CurrentUIApplication { get; set; } = new object();
    }
  }
}
