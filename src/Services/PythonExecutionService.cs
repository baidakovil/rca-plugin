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
        private UIApplication uiapp;

        /// <summary>
        /// Initializes a new instance of the <see cref="PythonExecutionService"/> class.
        /// </summary>
        public PythonExecutionService()
        {
            engine = Python.CreateEngine();
            scope = engine.CreateScope();
            engine.Runtime.LoadAssembly(typeof(Autodesk.Revit.DB.Document).Assembly); // RevitAPI.dll
            engine.Runtime.LoadAssembly(typeof(Autodesk.Revit.UI.UIDocument).Assembly); // RevitAPIUI.dll
        }

        /// <summary>
        /// Sets Revit API objects into the Python scope.
        /// </summary>
        public void SetRevitContext(UIApplication uiapp)
        {
            if (uiapp == null) throw new ArgumentNullException(nameof(uiapp));
            this.uiapp = uiapp;
        }

        /// <summary>
        /// Injects Revit context variables into the Python scope.
        /// </summary>
        private void InjectRevitContext()
        {
            if (uiapp == null)
                throw new InvalidOperationException("Revit context not set. Call SetRevitContext() first.");

            var activeUIDoc = uiapp.ActiveUIDocument;
            if (activeUIDoc == null)
                throw new InvalidOperationException("No active document in Revit.");

            AppDomain.CurrentDomain.SetData("uiapp", uiapp);
            AppDomain.CurrentDomain.SetData("uidoc", activeUIDoc);
            AppDomain.CurrentDomain.SetData("doc", activeUIDoc.Document);

            scope.SetVariable("uiapp", uiapp);
            scope.SetVariable("uidoc", activeUIDoc);
            scope.SetVariable("doc", activeUIDoc.Document);
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
                InjectRevitContext();
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
