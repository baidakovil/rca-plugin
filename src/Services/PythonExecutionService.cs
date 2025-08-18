using System;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;

namespace RcaPlugin.Services
{
    /// <summary>
    /// Service for executing Python code using IronPython3 and providing access to Revit API objects.
    /// </summary>
    public class PythonExecutionService
    {
        private readonly ScriptEngine engine;
        private readonly ScriptScope scope;

        /// <summary>
        /// Initializes a new instance of the <see cref="PythonExecutionService"/> class.
        /// </summary>
        public PythonExecutionService()
        {
            engine = Python.CreateEngine();
            scope = engine.CreateScope();
        }

        /// <summary>
        /// Sets Revit API objects into the Python scope.
        /// </summary>
        public void SetRevitContext(UIApplication uiapp)
        {
            if (uiapp == null) throw new ArgumentNullException(nameof(uiapp));
            scope.SetVariable("uiapp", uiapp);
            scope.SetVariable("uidoc", uiapp.ActiveUIDocument);
            scope.SetVariable("doc", uiapp.ActiveUIDocument?.Document);
        }

        /// <summary>
        /// Executes the given Python code asynchronously.
        /// </summary>
        /// <param name="code">Python code to execute.</param>
        /// <returns>Result of execution or exception message.</returns>
        public async Task<string> ExecuteAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return string.Empty;
            try
            {
                var source = engine.CreateScriptSourceFromString(code);
                var result = await Task.Run(() => source.Execute(scope));
                return result?.ToString() ?? "(no result)";
            }
            catch (Exception ex)
            {
                return $"Python Error: {ex.Message}";
            }
        }
    }
}
