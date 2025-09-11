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
            // Load the assembly
            var assembly = Assembly.LoadFrom(assemblyPath);
            
            // Find all classes with TestFixture attribute
            foreach (var type in assembly.GetTypes())
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