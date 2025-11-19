#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Rca.Loader.Infrastructure
{
  /// <summary>
  /// Strategies for loading assemblies in different contexts.
  /// </summary>
  public enum AssemblyLoadStrategy
  {
    /// <summary>
    /// Load assembly in the default application domain context.
    /// </summary>
    DefaultContext,

    /// <summary>
    /// Load assembly in the current custom load context.
    /// </summary>
    CurrentContext,

    /// <summary>
    /// Load assembly from the runtime directory into default context.
    /// </summary>
    RuntimeToDefault
  }

  /// <summary>
  /// Service for loading assemblies with different strategies and proper error handling.
  /// </summary>
  public class AssemblyLoadService
  {
    private readonly AssemblyLoadContext currentContext;
    private readonly string? runtimeBaseDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssemblyLoadService"/> class.
    /// </summary>
    /// <param name="currentContext">The current assembly load context.</param>
    /// <param name="runtimeBaseDirectory">The base directory for runtime assemblies.</param>
    public AssemblyLoadService(AssemblyLoadContext currentContext, string? runtimeBaseDirectory = null)
    {
      this.currentContext = currentContext ?? throw new ArgumentNullException(nameof(currentContext));
      this.runtimeBaseDirectory = runtimeBaseDirectory;
    }

    /// <summary>
    /// Attempts to load an assembly using the specified strategy.
    /// </summary>
    /// <param name="strategy">The loading strategy to use.</param>
    /// <param name="assemblyName">The name of the assembly to load.</param>
    /// <returns>The loaded assembly, or null if loading fails.</returns>
    public Assembly? TryLoad(AssemblyLoadStrategy strategy, string assemblyName)
    {
      if (string.IsNullOrWhiteSpace(assemblyName))
        return null;

      return strategy switch
      {
        AssemblyLoadStrategy.DefaultContext => LoadInDefaultContext(assemblyName),
        AssemblyLoadStrategy.CurrentContext => LoadInCurrentContext(assemblyName),
        AssemblyLoadStrategy.RuntimeToDefault => LoadFromRuntimeToDefault(assemblyName),
        _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown assembly load strategy")
      };
    }

    /// <summary>
    /// Checks if an assembly is already loaded in the default context.
    /// </summary>
    /// <param name="assemblyName">The name of the assembly to check.</param>
    /// <returns>The existing assembly if found, otherwise null.</returns>
    public Assembly? FindExistingInDefaultContext(string assemblyName)
    {
      if (string.IsNullOrWhiteSpace(assemblyName))
        return null;

      return AppDomain.CurrentDomain.GetAssemblies()
          .FirstOrDefault(assembly => !assembly.IsDynamic &&
              string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
    }

    private Assembly? LoadInDefaultContext(string assemblyName)
    {
      // Check if already loaded first
      var existingAssembly = FindExistingInDefaultContext(assemblyName);
      if (existingAssembly != null)
        return existingAssembly;

      // Try to load from runtime directory
      if (string.IsNullOrEmpty(runtimeBaseDirectory))
        return null;

      var assemblyPath = Path.Combine(runtimeBaseDirectory, assemblyName + AssemblyLoadConstants.DllExtension);
      if (!File.Exists(assemblyPath))
        return null;

      try
      {
        return AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
      }
      catch
      {
        // Swallow exceptions and return null for failed loads
        return null;
      }
    }

    private Assembly? LoadInCurrentContext(string assemblyName)
    {
      if (string.IsNullOrEmpty(runtimeBaseDirectory))
        return null;

      var assemblyPath = Path.Combine(runtimeBaseDirectory, assemblyName + AssemblyLoadConstants.DllExtension);
      if (!File.Exists(assemblyPath))
        return null;

      try
      {
        return currentContext.LoadFromAssemblyPath(assemblyPath);
      }
      catch
      {
        // Swallow exceptions and return null for failed loads
        return null;
      }
    }

    private Assembly? LoadFromRuntimeToDefault(string assemblyName)
    {
      if (string.IsNullOrEmpty(runtimeBaseDirectory))
        return null;

      var assemblyPath = Path.Combine(runtimeBaseDirectory, assemblyName + AssemblyLoadConstants.DllExtension);
      if (!File.Exists(assemblyPath))
        return null;

      try
      {
        return AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
      }
      catch
      {
        // Swallow exceptions and return null for failed loads
        return null;
      }
    }
  }
}
