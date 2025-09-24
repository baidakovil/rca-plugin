using System.Reflection;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Rca.TestAdapter;

/// <summary>
/// Helper class for discovering NUnit tests in assemblies.
/// </summary>
internal static class NUnitTestDiscoverer
{
    /// <summary>
    /// Finds all NUnit tests in the specified assembly.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly containing tests.</param>
    /// <returns>A list of test cases.</returns>
    public static IList<TestCase> FindTestsInAssembly(string assemblyPath)
    {
        var testCases = new List<TestCase>();
        
        try
        {
            // Load the assembly using simple approach
            var assembly = Assembly.LoadFrom(assemblyPath);
            
            // Get only loadable types to handle missing RevitAPI dependencies
            var types = GetLoadableTypes(assembly);
            
            // Find all classes with TestFixture attribute
            foreach (var type in types)
            {
                if (type.GetCustomAttributes(true).Any(a => a.GetType().Name == "TestFixtureAttribute"))
                {
                    // Find all methods with Test attribute
                    foreach (var method in type.GetMethods())
                    {
                        if (method.GetCustomAttributes(true).Any(a => a.GetType().Name == "TestAttribute"))
                        {
                            var testCase = CreateTestCase(assemblyPath, type, method);
                            testCases.Add(testCase);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error discovering tests in {assemblyPath}: {ex.Message}");
            throw;
        }
        
        return testCases;
    }
    
    /// <summary>
    /// Gets only the types that can be loaded from an assembly, handling ReflectionTypeLoadException.
    /// </summary>
    /// <param name="assembly">The assembly to get types from.</param>
    /// <returns>Array of successfully loaded types.</returns>
    private static Type[] GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Return only successfully loaded types, skip failed ones
            // This handles cases where RevitAPI dependencies are missing during test discovery
            return ex.Types.Where(t => t != null).ToArray()!;
        }
    }
    
    private static TestCase CreateTestCase(string assemblyPath, Type type, MethodInfo method)
    {
        // Create a fully qualified name for the test
        var fullyQualifiedName = $"{type.FullName}.{method.Name}";
        
        // Create the test case
        var testCase = new TestCase(fullyQualifiedName, new Uri(Constants.ExecutorUri), assemblyPath)
        {
            DisplayName = method.Name,
            CodeFilePath = null, // Could determine this with PDB information if needed
            LineNumber = 0,      // Could determine this with PDB information if needed
        };
        
        // Add any traits from Test categories
        var categoryAttributes = method.GetCustomAttributes(true)
            .Concat(type.GetCustomAttributes(true))
            .Where(a => a.GetType().Name == "CategoryAttribute");
        
        foreach (var attr in categoryAttributes)
        {
            var categoryName = attr.GetType().GetProperty("Name")?.GetValue(attr) as string;
            if (!string.IsNullOrEmpty(categoryName))
            {
                testCase.Traits.Add(new Trait("Category", categoryName));
            }
        }
        
        return testCase;
    }
}
