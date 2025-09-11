using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using NUnit.Framework;
using NUnit.Framework.Internal;
using System.Diagnostics;

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
            Debug.WriteLine("DEBUG: RevitTestExecutor initialized with UIApplication");
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
            Debug.WriteLine($"DEBUG: ExecuteTests called for {testRequests.Count} tests in {assemblyPath}");
            
            try
            {
                // Load the test assembly
                Debug.WriteLine($"DEBUG: Loading test assembly from {assemblyPath}");
                var assembly = Assembly.LoadFrom(assemblyPath);
                Debug.WriteLine($"DEBUG: Assembly loaded: {assembly.FullName}");
                
                // Execute each test
                foreach (var testRequest in testRequests)
                {
                    Debug.WriteLine($"DEBUG: Executing test: {testRequest.FullyQualifiedName}");
                    var testResult = ExecuteTest(assembly, testRequest);
                    Debug.WriteLine($"DEBUG: Test execution complete, outcome: {testResult.Outcome}");
                    results.Add(testResult);
                }
                
                Debug.WriteLine($"DEBUG: All tests executed, total results: {results.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DEBUG: Error executing tests: {ex.Message}");
                Debug.WriteLine($"DEBUG: Stack trace: {ex.StackTrace}");
                
                // Handle global error
                var errorResult = new TestResult
                {
                    FullyQualifiedName = "Assembly.Load",
                    DisplayName = "Assembly Load Error",
                    Outcome = "Failed",
                    ErrorMessage = $"Failed to load or process assembly: {ex.Message}",
                    ErrorStackTrace = ex.StackTrace ?? "",
                    StartTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    EndTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    DurationInMilliseconds = 0,
                    Messages = new List<TestMessage>
                    {
                        new TestMessage
                        {
                            Level = "Error",
                            Text = ex.ToString()
                        }
                    }
                };
                results.Add(errorResult);
            }
            
            return results;
        }
        
        private TestResult ExecuteTest(Assembly assembly, TestRequest testRequest)
        {
            Debug.WriteLine($"DEBUG: ExecuteTest starting for {testRequest.FullyQualifiedName}");
            
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
                // Parse the fully qualified name to get the class and method
                var lastDot = testRequest.FullyQualifiedName.LastIndexOf('.');
                if (lastDot <= 0 || lastDot >= testRequest.FullyQualifiedName.Length - 1)
                {
                    throw new ArgumentException($"Invalid fully qualified name: {testRequest.FullyQualifiedName}");
                }
                
                var className = testRequest.FullyQualifiedName.Substring(0, lastDot);
                var methodName = testRequest.FullyQualifiedName.Substring(lastDot + 1);
                
                Debug.WriteLine($"DEBUG: Class name: {className}, Method name: {methodName}");
                
                // Get the test class type
                var testClassType = assembly.GetType(className);
                if (testClassType == null)
                {
                    Debug.WriteLine($"DEBUG: Type not found: {className}");
                    
                    // Log available types for debugging
                    var availableTypes = string.Join(", ", assembly.GetTypes().Select(t => t.FullName));
                    Debug.WriteLine($"DEBUG: Available types: {availableTypes}");
                    
                    throw new ArgumentException($"Type not found: {className}");
                }
                
                // Get the test method
                var testMethod = testClassType.GetMethod(methodName);
                if (testMethod == null)
                {
                    Debug.WriteLine($"DEBUG: Method not found: {methodName}");
                    throw new ArgumentException($"Method not found: {methodName}");
                }
                
                // Create an instance of the test class
                // Check if the class inherits from UIApplicationTests
                Debug.WriteLine("DEBUG: Checking if test class inherits from UIApplicationTests");
                var needsUiApp = IsSubclassOf(testClassType, "UIApplicationTests");
                Debug.WriteLine($"DEBUG: Needs UIApplication: {needsUiApp}");
                
                object testInstance;
                
                if (needsUiApp)
                {
                    // Call GlobalSetup with UIApplication
                    Debug.WriteLine("DEBUG: Creating test instance and calling GlobalSetup");
                    testInstance = Activator.CreateInstance(testClassType)!;
                    var setupMethod = testClassType.GetMethod("GlobalSetup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    
                    if (setupMethod == null)
                    {
                        Debug.WriteLine("DEBUG: GlobalSetup method not found");
                    }
                    else
                    {
                        Debug.WriteLine("DEBUG: Invoking GlobalSetup with UIApplication");
                        setupMethod.Invoke(testInstance, new[] { uiapp });
                    }
                    
                    // Check for SetUp method
                    Debug.WriteLine("DEBUG: Looking for SetUp methods");
                    var setupMethods = testClassType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(m => m.GetCustomAttributes(true).Any(a => a.GetType().Name == "SetUpAttribute"))
                        .ToList();
                    
                    Debug.WriteLine($"DEBUG: Found {setupMethods.Count} SetUp methods");
                    foreach (var setup in setupMethods)
                    {
                        Debug.WriteLine($"DEBUG: Invoking SetUp method: {setup.Name}");
                        setup.Invoke(testInstance, null);
                    }
                }
                else
                {
                    // Regular instantiation
                    Debug.WriteLine("DEBUG: Creating regular test instance");
                    testInstance = Activator.CreateInstance(testClassType)!;
                    
                    // Check for SetUp method
                    Debug.WriteLine("DEBUG: Looking for SetUp methods");
                    var setupMethods = testClassType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(m => m.GetCustomAttributes(true).Any(a => a.GetType().Name == "SetUpAttribute"))
                        .ToList();
                    
                    Debug.WriteLine($"DEBUG: Found {setupMethods.Count} SetUp methods");
                    foreach (var setup in setupMethods)
                    {
                        Debug.WriteLine($"DEBUG: Invoking SetUp method: {setup.Name}");
                        setup.Invoke(testInstance, null);
                    }
                }
                
                // Invoke the test method
                Debug.WriteLine($"DEBUG: Invoking test method: {methodName}");
                testMethod.Invoke(testInstance, null);
                
                // Test passed
                Debug.WriteLine("DEBUG: Test passed");
                result.Outcome = "Passed";
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                // Test failed with an assertion or other exception
                Debug.WriteLine($"DEBUG: Test failed with inner exception: {ex.InnerException.Message}");
                result.Outcome = "Failed";
                result.ErrorMessage = ex.InnerException.Message;
                result.ErrorStackTrace = ex.InnerException.StackTrace ?? "";
                
                result.Messages.Add(new TestMessage
                {
                    Level = "Error",
                    Text = ex.InnerException.ToString()
                });
            }
            catch (Exception ex)
            {
                // Test failed with a different exception
                Debug.WriteLine($"DEBUG: Test failed with exception: {ex.Message}");
                result.Outcome = "Failed";
                result.ErrorMessage = ex.Message;
                result.ErrorStackTrace = ex.StackTrace ?? "";
                
                result.Messages.Add(new TestMessage
                {
                    Level = "Error",
                    Text = ex.ToString()
                });
            }
            
            // Calculate duration
            var endTime = DateTimeOffset.UtcNow;
            result.EndTimeUnixMs = endTime.ToUnixTimeMilliseconds();
            result.DurationInMilliseconds = (endTime - startTime).TotalMilliseconds;
            
            Debug.WriteLine($"DEBUG: Test execution completed in {result.DurationInMilliseconds}ms, outcome: {result.Outcome}");
            return result;
        }
        
        private bool IsSubclassOf(Type type, string baseClassName)
        {
            if (type == null) return false;
            
            // Check if this type is the base class
            if (type.Name == baseClassName) return true;
            
            // Check the base type
            if (type.BaseType != null)
            {
                return IsSubclassOf(type.BaseType, baseClassName);
            }
            
            return false;
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