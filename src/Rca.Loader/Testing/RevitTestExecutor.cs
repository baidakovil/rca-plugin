using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using System.Diagnostics;
using System.Runtime.Loader;

namespace Rca.Loader.Testing
{
    /// <summary>
    /// Service for executing tests in the Revit context.
    /// </summary>
    public class RevitTestExecutor
    {
        private readonly UIApplication uiapp;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="RevitTestExecutor"/> class.
        /// </summary>
        /// <param name="uiapp">The Revit UI application.</param>
        public RevitTestExecutor(UIApplication uiapp)
        {
            this.uiapp = uiapp ?? throw new ArgumentNullException(nameof(uiapp));
        }
        
        /// <summary>
        /// Executes the specified tests.
        /// </summary>
        /// <param name="assemblyPath">Path to the test assembly.</param>
        /// <param name="testRequests">The tests to execute.</param>
        /// <returns>Test results.</returns>
        public List<TestResult> ExecuteTests(string assemblyPath, List<TestRequest> testRequests)
        {
            var results = new List<TestResult>();
            
            try
            {
                var assembly = LoadTestAssembly(assemblyPath);
                
                foreach (var testRequest in testRequests)
                {
                    var testResult = ExecuteTest(assembly, testRequest);
                    results.Add(testResult);
                }
            }
            catch (Exception ex)
            {
                results.Add(CreateErrorResult("Assembly.Load", "Assembly Load Error", ex));
            }
            
            return results;
        }
        
        private Assembly LoadTestAssembly(string assemblyPath)
        {
            // Load test assembly in default context to avoid assembly loading conflicts
            using (AssemblyLoadContext.Default.EnterContextualReflection())
            {
                return Assembly.LoadFrom(assemblyPath);
            }
        }
        
        private TestResult ExecuteTest(Assembly assembly, TestRequest testRequest)
        {
            var result = new TestResult
            {
                FullyQualifiedName = testRequest.FullyQualifiedName,
                DisplayName = testRequest.DisplayName,
                Messages = new List<TestMessage>()
            };
            
            var startTime = DateTimeOffset.UtcNow;
            result.StartTimeUnixMs = startTime.ToUnixTimeMilliseconds();
            
            try
            {
                var (testClassType, testMethod) = ParseAndGetTestMethod(assembly, testRequest.FullyQualifiedName);
                var testInstance = CreateTestInstance(testClassType);
                
                RunSetupMethods(testInstance, testClassType);
                
                // Execute the test method
                testMethod.Invoke(testInstance, null);
                
                result.Outcome = "Passed";
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                SetTestFailure(result, ex.InnerException);
            }
            catch (Exception ex)
            {
                SetTestFailure(result, ex);
            }
            
            var endTime = DateTimeOffset.UtcNow;
            result.EndTimeUnixMs = endTime.ToUnixTimeMilliseconds();
            result.DurationInMilliseconds = (endTime - startTime).TotalMilliseconds;
            
            return result;
        }
        
        private (Type testClassType, MethodInfo testMethod) ParseAndGetTestMethod(Assembly assembly, string fullyQualifiedName)
        {
            var lastDot = fullyQualifiedName.LastIndexOf('.');
            if (lastDot <= 0 || lastDot >= fullyQualifiedName.Length - 1)
            {
                throw new ArgumentException($"Invalid fully qualified name: {fullyQualifiedName}");
            }
            
            var className = fullyQualifiedName.Substring(0, lastDot);
            var methodName = fullyQualifiedName.Substring(lastDot + 1);
            
            var testClassType = assembly.GetType(className) 
                ?? throw new ArgumentException($"Type not found: {className}");
            
            var testMethod = testClassType.GetMethod(methodName) 
                ?? throw new ArgumentException($"Method not found: {methodName}");
            
            return (testClassType, testMethod);
        }
        
        private object CreateTestInstance(Type testClassType)
        {
            var testInstance = Activator.CreateInstance(testClassType)!;
            
            // If test class inherits from UIApplicationTests, call GlobalSetup
            if (IsSubclassOf(testClassType, "UIApplicationTests"))
            {
                var setupMethod = testClassType.GetMethod("GlobalSetup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                setupMethod?.Invoke(testInstance, new[] { uiapp });
            }
            
            return testInstance;
        }
        
        private void RunSetupMethods(object testInstance, Type testClassType)
        {
            var setupMethods = testClassType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.GetCustomAttributes(true).Any(a => a.GetType().Name == "SetUpAttribute"));
            
            foreach (var setup in setupMethods)
            {
                setup.Invoke(testInstance, null);
            }
        }
        
        private void SetTestFailure(TestResult result, Exception ex)
        {
            result.Outcome = "Failed";
            result.ErrorMessage = ex.Message;
            result.ErrorStackTrace = ex.StackTrace ?? "";
            result.Messages.Add(new TestMessage
            {
                Level = "Error",
                Text = ex.ToString()
            });
        }
        
        private TestResult CreateErrorResult(string fullyQualifiedName, string displayName, Exception ex)
        {
            return new TestResult
            {
                FullyQualifiedName = fullyQualifiedName,
                DisplayName = displayName,
                Outcome = "Failed",
                ErrorMessage = $"Failed to load or process assembly: {ex.Message}",
                ErrorStackTrace = ex.StackTrace ?? "",
                StartTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                EndTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                DurationInMilliseconds = 0,
                Messages = new List<TestMessage>
                {
                    new TestMessage { Level = "Error", Text = ex.ToString() }
                }
            };
        }
        
        private bool IsSubclassOf(Type type, string baseClassName)
        {
            if (type == null) return false;
            if (type.Name == baseClassName) return true;
            return type.BaseType != null && IsSubclassOf(type.BaseType, baseClassName);
        }
        
        /// <summary>
        /// Test request from the pipe.
        /// </summary>
        public class TestRequest
        {
            /// <summary>
            /// Gets or sets the fully qualified name of the test.
            /// </summary>
            public string FullyQualifiedName { get; set; } = string.Empty;
            
            /// <summary>
            /// Gets or sets the display name of the test.
            /// </summary>
            public string DisplayName { get; set; } = string.Empty;
        }
        
        /// <summary>
        /// Test result sent back through the pipe.
        /// </summary>
        public class TestResult
        {
            /// <summary>
            /// Gets or sets the fully qualified name of the test.
            /// </summary>
            public string FullyQualifiedName { get; set; } = string.Empty;
            
            /// <summary>
            /// Gets or sets the display name of the test.
            /// </summary>
            public string DisplayName { get; set; } = string.Empty;
            
            /// <summary>
            /// Gets or sets the outcome of the test.
            /// </summary>
            public string Outcome { get; set; } = string.Empty;
            
            /// <summary>
            /// Gets or sets the error message.
            /// </summary>
            public string ErrorMessage { get; set; } = string.Empty;
            
            /// <summary>
            /// Gets or sets the error stack trace.
            /// </summary>
            public string ErrorStackTrace { get; set; } = string.Empty;
            
            /// <summary>
            /// Gets or sets the duration in milliseconds.
            /// </summary>
            public double DurationInMilliseconds { get; set; }
            
            /// <summary>
            /// Gets or sets the start time in Unix milliseconds.
            /// </summary>
            public long StartTimeUnixMs { get; set; }
            
            /// <summary>
            /// Gets or sets the end time in Unix milliseconds.
            /// </summary>
            public long EndTimeUnixMs { get; set; }
            
            /// <summary>
            /// Gets or sets the messages.
            /// </summary>
            public List<TestMessage> Messages { get; set; } = new();
        }
        
        /// <summary>
        /// Message from test execution.
        /// </summary>
        public class TestMessage
        {
            /// <summary>
            /// Gets or sets the message level.
            /// </summary>
            public string Level { get; set; } = "Informational";
            
            /// <summary>
            /// Gets or sets the message text.
            /// </summary>
            public string Text { get; set; } = string.Empty;
        }
        
        /// <summary>
        /// Payload for test execution.
        /// </summary>
        public class TestExecutionPayload
        {
            /// <summary>
            /// Gets or sets the assembly path.
            /// </summary>
            public string AssemblyPath { get; set; } = string.Empty;
            
            /// <summary>
            /// Gets or sets the tests to execute.
            /// </summary>
            public List<TestRequest> Tests { get; set; } = new();
        }
    }
}