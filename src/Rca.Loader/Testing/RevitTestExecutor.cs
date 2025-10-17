using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.IO;
using Autodesk.Revit.UI;
using Microsoft.Extensions.Logging;
using Rca.Loader.Logging;

namespace Rca.Loader.Testing
{
    /// <summary>
    /// Service for executing tests in the Revit context.
    /// Loads test assemblies into a collectible AssemblyLoadContext to avoid file locks
    /// and enable reloading without restarting Revit.
    /// </summary>
    public class RevitTestExecutor
    {
        private static readonly ILogger Log = LoaderLog.GetLogger<RevitTestExecutor>();

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
                    Log.LogInformation("Forcing unload of active TestLoadContext");
                    ctx.Unload();
                    // Promote finalization of collectible assemblies
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning(ex, "Error while force-unloading TestLoadContext (ignored)");
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

            Log.LogInformation("Starting test execution count={Count} assembly={Assembly}", testRequests.Count, assemblyPath);

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
                    Log.LogDebug("Loaded test assembly {Name} from {Path}", assembly.GetName().Name, assemblyPath);

                    foreach (var testRequest in testRequests)
                    {
                        var testResult = ExecuteTest(assembly, testRequest);
                        results.Add(testResult);
                    }
                }
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                Log.LogError(ex.InnerException, "Test invocation error");
                results.Add(CreateErrorResult("Test.Invoke", "Test Execution Error", ex.InnerException));
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "Test execution error during assembly load");
                results.Add(CreateErrorResult("Assembly.Load", "Assembly Load Error", ex));
            }
            finally
            {
                // Unload the test ALC to release file locks
                if (testAlc != null)
                {
                    try
                    {
                        Log.LogInformation("Unloading TestLoadContext after test run");
                        testAlc.Unload();
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning(ex, "Error while unloading TestLoadContext (ignored)");
                    }
                    // Promote collection of collectible assemblies
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }
                activeTestAlc = null;
            }

            Log.LogInformation("Completed test execution count={Count}", results.Count);
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
                Log.LogDebug("Preparing to execute test {FQN}", testRequest.FullyQualifiedName);
                var (testClassType, testMethod) = ParseAndGetTestMethod(assembly, testRequest.FullyQualifiedName);
                var testInstance = CreateTestInstance(testClassType);

                RunSetupMethods(testInstance, testClassType);

                // Execute the test method
                testMethod.Invoke(testInstance, null);

                result.Outcome = "Passed";
                Log.LogDebug("Test passed {FQN}", testRequest.FullyQualifiedName);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                Log.LogWarning(ex.InnerException, "Test failed (inner) {FQN}", testRequest.FullyQualifiedName);
                SetTestFailure(result, ex.InnerException);
            }
            catch (Exception ex)
            {
                Log.LogWarning(ex, "Test failed {FQN}", testRequest.FullyQualifiedName);
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
                Log.LogTrace("Invoking [SetUp] {Method} on {Type}", setup.Name, testClassType.FullName);
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
            public string FullyQualifiedName { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
        }

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

        public class TestMessage
        {
            public string Level { get; set; } = "Informational";
            public string Text { get; set; } = string.Empty;
        }

        public class TestExecutionPayload
        {
            public string AssemblyPath { get; set; } = string.Empty;
            public List<TestRequest> Tests { get; set; } = new();
        }

        #endregion

        /// <summary>
        /// Collectible test AssemblyLoadContext with path-based dependency resolution.
        /// Resolution order:
        /// - For Rca.* assemblies: always reuse an already loaded assembly (Runtime ALC) to avoid duplicates;
        ///   only if none is loaded, let default context handle it.
        /// - For other assemblies: try resolver (deps.json), then probe next to the test assembly, then default context.
        /// Logs each resolution decision.
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
                Log.LogDebug("Created TestLoadContext for {Path}", assemblyPath);
            }

            protected override Assembly? Load(AssemblyName assemblyName)
            {
                try
                {
                    // 0) Prefer reusing Runtime-loaded RCA assemblies regardless of local files
                    if (!string.IsNullOrEmpty(assemblyName.Name) && assemblyName.Name.StartsWith("Rca.", StringComparison.OrdinalIgnoreCase))
                    {
                        var loaded = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));
                        if (loaded != null)
                        {
                            Log.LogDebug("Reusing loaded RCA assembly {Name} from runtime context (Location={Location})", assemblyName.Name, SafeLocation(loaded));
                            return loaded;
                        }
                        Log.LogWarning("RCA assembly requested but not found in runtime: {Name}. Delegating to default context.", assemblyName.Name);
                        return null;
                    }

                    // 1) Use resolver (deps.json) for non-RCA dependencies
                    var path = resolver.ResolveAssemblyToPath(assemblyName);
                    if (!string.IsNullOrEmpty(path))
                    {
                        Log.LogDebug("Resolved dependency via deps.json {Name} -> {Path}", assemblyName.Name, path);
                        return LoadFromAssemblyPath(path);
                    }

                    // 2) Probe next to the test assembly for dependencies like nunit.framework / FluentAssertions
                    var candidate = Path.Combine(baseDir, assemblyName.Name + ".dll");
                    if (File.Exists(candidate))
                    {
                        Log.LogDebug("Resolved dependency from test folder {Name} -> {Path}", assemblyName.Name, candidate);
                        return LoadFromAssemblyPath(candidate);
                    }

                    // 3) Fallback to default context for shared/runtime-provided assemblies (e.g., RevitAPI)
                    Log.LogTrace("Delegating dependency to default context {Name}", assemblyName.Name);
                    return null;
                }
                catch (Exception ex)
                {
                    Log.LogWarning(ex, "Error resolving assembly {Name} in TestLoadContext", assemblyName.Name);
                    return null;
                }
            }

            protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
            {
                try
                {
                    var path = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
                    if (!string.IsNullOrEmpty(path))
                    {
                        Log.LogDebug("Resolved native via deps.json {Name} -> {Path}", unmanagedDllName, path);
                        return LoadUnmanagedDllFromPath(path);
                    }

                    var candidate = Path.Combine(baseDir, unmanagedDllName + ".dll");
                    if (File.Exists(candidate))
                    {
                        Log.LogDebug("Resolved native from test folder {Name} -> {Path}", unmanagedDllName, candidate);
                        return LoadUnmanagedDllFromPath(candidate);
                    }

                    Log.LogTrace("Delegating native to default context {Name}", unmanagedDllName);
                    return IntPtr.Zero;
                }
                catch (Exception ex)
                {
                    Log.LogWarning(ex, "Error resolving native library {Name} in TestLoadContext", unmanagedDllName);
                    return IntPtr.Zero;
                }
            }

            private static string SafeLocation(Assembly asm)
            {
                try { return asm.IsDynamic ? "<dynamic>" : (string.IsNullOrEmpty(asm.Location) ? "<unknown>" : asm.Location); }
                catch { return "<unavailable>"; }
            }
        }
    }
}
