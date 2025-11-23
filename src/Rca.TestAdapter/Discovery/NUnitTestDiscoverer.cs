using System.Reflection;
using System.Runtime.Loader;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Rca.TestAdapter;

/// <summary>
/// Helper class for discovering NUnit tests in assemblies.
/// Loads assemblies into a collectible ALC to avoid file locks during discovery in VS process.
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

    DiscoveryLoadContext? alc = null;
    try
    {
      alc = new DiscoveryLoadContext(assemblyPath);
      using (alc.EnterContextualReflection())
      {
        var assembly = alc.LoadFromAssemblyPath(assemblyPath);
        var factory = new NUnitTestCaseFactory(assemblyPath);
        var discovered = factory.CreateTestCases(assembly);
        testCases.AddRange(discovered);
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error discovering tests in {assemblyPath}: {ex.Message}");
      throw;
    }
    finally
    {
      if (alc != null)
      {
        try { alc.Unload(); } catch { }
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
      }
    }

    return testCases;
  }

  /// <summary>
  /// A collectible ALC for discovery-time isolation.
  /// </summary>
  private sealed class DiscoveryLoadContext : AssemblyLoadContext
  {
    private readonly AssemblyDependencyResolver resolver;
    private readonly string baseDir;

    public DiscoveryLoadContext(string assemblyPath) : base(isCollectible: true)
    {
      resolver = new AssemblyDependencyResolver(assemblyPath);
      baseDir = Path.GetDirectoryName(assemblyPath)!;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
      var path = resolver.ResolveAssemblyToPath(assemblyName);
      if (!string.IsNullOrEmpty(path))
        return LoadFromAssemblyPath(path);

      // Fallback: look for DLL next to the assembly
      var candidate = System.IO.Path.Combine(baseDir, assemblyName.Name + ".dll");
      if (File.Exists(candidate))
        return LoadFromAssemblyPath(candidate);

      return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
      var path = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
      if (!string.IsNullOrEmpty(path))
        return LoadUnmanagedDllFromPath(path);
      return IntPtr.Zero;
    }
  }

  /// <summary>
  /// Builds <see cref="TestCase"/> instances from a loaded NUnit test assembly.
  /// </summary>
  private sealed class NUnitTestCaseFactory
  {
    private readonly string _assemblyPath;

    public NUnitTestCaseFactory(string assemblyPath)
    {
      ArgumentNullException.ThrowIfNull(assemblyPath);
      _assemblyPath = assemblyPath;
    }

    /// <summary>
    /// Creates test cases for all NUnit <c>[TestFixture]</c> and <c>[Test]</c> combinations.
    /// </summary>
    /// <param name="assembly">The loaded test assembly.</param>
    /// <returns>List of discovered <see cref="TestCase"/> objects.</returns>
    public List<TestCase> CreateTestCases(Assembly assembly)
    {
      var result = new List<TestCase>();
      var types = GetLoadableTypes(assembly);

      foreach (var type in types)
      {
        if (!HasAttribute(type.GetCustomAttributes(true), "TestFixtureAttribute"))
        {
          continue;
        }

        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
          if (!HasAttribute(method.GetCustomAttributes(true), "TestAttribute"))
          {
            continue;
          }

          var testCase = CreateTestCase(type, method);
          result.Add(testCase);
        }
      }

      return result;
    }

    private static bool HasAttribute(object[] attributes, string attributeName)
    {
      return attributes.Any(a => a.GetType().Name == attributeName);
    }

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

    private TestCase CreateTestCase(Type type, MethodInfo method)
    {
      var fullyQualifiedName = $"{type.FullName}.{method.Name}";

      var testCase = new TestCase(fullyQualifiedName, new Uri(Constants.ExecutorUri), _assemblyPath)
      {
        DisplayName = method.Name,
        CodeFilePath = null,
        LineNumber = 0,
      };

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
}


