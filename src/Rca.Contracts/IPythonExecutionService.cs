using System.Threading.Tasks;

namespace Rca.Contracts
{
    /// <summary>
    /// Interface for Python execution service abstraction.
    /// </summary>
    public interface IPythonExecutionService
    {
        /// <summary>
        /// Sets the Revit context for Python execution.
        /// </summary>
        /// <param name="context">The Revit context object.</param>
        void SetRevitContext(object context);

        /// <summary>
        /// Executes Python code asynchronously via ExternalEvent for safe UI thread marshaling.
        /// Preferred method for execution from UI components.
        /// </summary>
        /// <param name="code">The Python code to execute.</param>
        /// <returns>The execution result.</returns>
        Task<string> ExecuteAsync(string code);

        /// <summary>
        /// Executes Python code synchronously. Should only be called from within a Revit API context.
        /// Used primarily for test execution and direct API calls.
        /// </summary>
        /// <param name="code">The Python code to execute.</param>
        /// <returns>The execution result.</returns>
        string ExecuteSync(string code);

        /// <summary>
        /// Executes Python code with automatic path selection based on current thread context.
        /// Uses ExecuteAsync for UI threads, ExecuteSync for Revit API threads.
        /// </summary>
        /// <param name="code">The Python code to execute.</param>
        /// <returns>The execution result.</returns>
        Task<string> ExecuteSmartAsync(string code);
    }
}