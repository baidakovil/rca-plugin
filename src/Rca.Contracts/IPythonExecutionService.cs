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
        /// Executes Python code asynchronously.
        /// </summary>
        /// <param name="code">The Python code to execute.</param>
        /// <returns>The execution result.</returns>
        Task<string> ExecuteAsync(string code);
    }
}