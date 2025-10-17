using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.IO;
using Autodesk.Revit.UI;

namespace Rca.Loader.Testing
{
    /// <summary>
    /// Service for executing tests in the Revit context.
    /// Loads test assemblies into a collectible AssemblyLoadContext to avoid file locks
    /// and enable reloading without restarting Revit.
    /// </summary>
    public class RevitTestExecutor
    {
        private readonly UIApplication uiapp;

        // Weak reference to the last active test ALC for forced unload on ReloadRuntime
        private static WeakReference<TestLoadContext>? activeTestAlc;

        /// <summary>
        /// Initializes a new instance of the <see cref="RevitTestExecutor"/> class.
        /// </summary>
        /// <param name="uiapp">The Revit UI application.</param>
        public RevitTestExecutor(UIApplication uiapp)
        {
            this.uiapp = uiapp ?? throw new ArgumentNullException(nameof(uiapp));
        }

        /// <summary>
        /// Forces unload of an active test AssemblyLoadContext, if any.
        /// Used by ReloadRuntime to guarantee test context cleanup.
        /// </summary>
        public static void ForceUnloadActiveTestLoadContext()
        {
            try
            {
                if (activeTestAlc != null && activeTestAlc.TryGetTarget(out var ctx))
                {
                    ctx.Unload();
                    // Promote finalization of collectible assemblies
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }
            }
            catch
            {
                // Best-effort cleanup; failures are non-fatal
            }
            finally
            {
                activeTestAlc = null;
            }
        }

        /// <summary>
        /// Executes the specified tests.
        /// </summary>
        /// <param name="assemblyPath">Path to the test assembly (in the latest runtime folder).</param>
        /// <param name="testRequests">The tests to execute.</param>
        /// <returns>Test results.</returns>
        public List<TestResult> ExecuteTests(string assemblyPath, List<TestRequest> testRequests)
        {
            if (testRequests == null)
                throw new ArgumentNullException(nameof(testRequests));

            var results = new List<TestResult>();
            TestLoadContext? testAlc = null;

            try
            {
                testAlc = new TestLoadContext(assemblyPath);
                activeTestAlc = new WeakReference<TestLoadContext>(testAlc);

                // Enter contextual reflection for the test ALC to ensure correct type resolution
                using (testAlc.EnterContextualReflection())
                {
                    var assembly = testAlc.LoadFromAssemblyPath(assemblyPath);

                    foreach (var testRequest in testRequests)
                    {
                        var testResult = ExecuteTest(assembly, testRequest);
                        results.Add(testResult);
                    }
                }
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                results.Add(CreateErrorResult("Test.Invoke", "Test Execution Error", ex.InnerException));
            }
            catch (Exception ex)
            {
                results.Add(CreateErrorResult("Assembly.Load", "Assembly Load Error", ex));
            }
            finally
            {
                // Unload the test ALC to release file locks
                if (testAlc != null)
                {
                    try { testAlc.Unload(); } catch { }
                    // Promote collection of collectible assemblies
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }
                activeTestAlc = null;
            }

            return results;
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
            var lastDot = fullyQualifiedName.LastIndexOf('.') ;
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

            // If test class has GlobalSetup method, call it with UIApplication
            var setupMethod = testClassType.GetMethod("GlobalSetup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            setupMethod?.Invoke(testInstance, new[] { uiapp });

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
            result.ErrorStackTrace = ex.StackTrace ?? string.Empty;
            result.Messages.Add(new TestMessage
            {
                Level = "Error",
                Text = ex.ToString()
            });
        }

        private TestResult CreateErrorResult(string fullyQualifiedName, string displayName, Exception ex) =>
            new TestResult
            {
                FullyQualifiedName = fullyQualifiedName,
                DisplayName = displayName,
                Outcome = "Failed",
                ErrorMessage = $"Failed to load or process assembly: {ex.Message}",
                ErrorStackTrace = ex.StackTrace ?? string.Empty,
                StartTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                EndTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                DurationInMilliseconds = 0,
                Messages = new List<TestMessage>
                {
                    new TestMessage { Level = "Error", Text = ex.ToString() }
                }
            };

        #region Data Transfer Objects

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
            public string FullyQualifiedName { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string Outcome { get; set; } = string.Empty;
            public string ErrorMessage { get; set; } = string.Empty;
            public string ErrorStackTrace { get; set; } = string.Empty;
            public double DurationInMilliseconds { get; set; }
            public long StartTimeUnixMs { get; set; }
            public long EndTimeUnixMs { get; set; }
            public List<TestMessage> Messages { get; set; } = new();
        }

        /// <summary>
        /// Message from test execution.
        /// </summary>
        public class TestMessage
        {
            public string Level { get; set; } = "Informational";
            public string Text { get; set; } = string.Empty;
        }

        /// <summary>
        /// Payload for test execution.
        /// </summary>
        public class TestExecutionPayload
        {
            public string AssemblyPath { get; set; } = string.Empty;
            public List<TestRequest> Tests { get; set; } = new();
        }

        #endregion

        /// <summary>
        /// Collectible test AssemblyLoadContext with path-based dependency resolution.
        /// Tries resolver first (deps.json), then falls back to probing in the test assembly directory.
        /// </summary>
        private sealed class TestLoadContext : AssemblyLoadContext
        {
            private readonly AssemblyDependencyResolver resolver;
            private readonly string baseDir;

            public TestLoadContext(string assemblyPath) : base(isCollectible: true)
            {
                if (string.IsNullOrEmpty(assemblyPath)) throw new ArgumentNullException(nameof(assemblyPath));
                resolver = new AssemblyDependencyResolver(assemblyPath);
                baseDir = Path.GetDirectoryName(assemblyPath)!;
            }

            protected override Assembly? Load(AssemblyName assemblyName)
            {
                var path = resolver.ResolveAssemblyToPath(assemblyName);
                if (!string.IsNullOrEmpty(path))
                {
                    return LoadFromAssemblyPath(path);
                }

                // Fallback: probe next to the test assembly for dependencies like nunit.framework.dll
                var candidate = Path.Combine(baseDir, assemblyName.Name + ".dll");
                if (File.Exists(candidate))
                {
                    return LoadFromAssemblyPath(candidate);
                }

                // Fallback to default context for shared/runtime-provided assemblies (e.g., RevitAPI)
                return null;
            }

            protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
            {
                var path = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
                if (!string.IsNullOrEmpty(path))
                {
                    return LoadUnmanagedDllFromPath(path);
                }
                // Optional: probe next to the assembly
                var candidate = Path.Combine(baseDir, unmanagedDllName + ".dll");
                if (File.Exists(candidate))
                {
                    return LoadUnmanagedDllFromPath(candidate);
                }
                return IntPtr.Zero;
            }
        }
    }
}
