#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using Rca.Loader.Logging;

namespace Rca.Loader.Infrastructure
{
  /// <summary>
  /// Custom assembly load context for loading runtime assemblies in an isolated, unloadable context.
  /// </summary>
  public class RuntimeLoadContext : AssemblyLoadContext
  {
    private AssemblyLoadService? assemblyLoader;
    private string? runtimePath;
    private object? runtimeInstance;
    private bool disposed = false;
    private static readonly ILogger Log = LoaderLog.GetLogger<RuntimeLoadContext>();

    /// <summary>
    /// Gets the path to the runtime assembly.
    /// </summary>
    public string RuntimePath => runtimePath ?? string.Empty;

    /// <summary>
    /// Gets the runtime instance.
    /// </summary>
    public object? RuntimeInstance => runtimeInstance;

    /// <summary>
    /// Sets the path to the runtime assembly.
    /// </summary>
    /// <param name="path">The path to the runtime assembly.</param>
    public void SetRuntimePath(string path)
    {
      runtimePath = path;
      Log.LogDebug("RuntimeLoadContext.SetRuntimePath path={Path}", path);

      var baseDirectory = !string.IsNullOrEmpty(path) ? Path.GetDirectoryName(path) : null;
      assemblyLoader = new AssemblyLoadService(this, baseDirectory);
    }

    /// <summary>
    /// Sets the runtime instance.
    /// </summary>
    /// <param name="instance">The runtime instance to set.</param>
    public void SetRuntimeInstance(object instance)
    {
      runtimeInstance = instance;
      Log.LogTrace("Runtime instance set type={Type}", instance?.GetType().FullName);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeLoadContext"/> class.
    /// </summary>
    public RuntimeLoadContext() : base(isCollectible: true)
    {
      assemblyLoader = new AssemblyLoadService(this);
      Resolving += OnResolving;
      AssemblyLoadContext.Default.Resolving += OnDefaultContextResolving;
      Log.LogDebug("RuntimeLoadContext created Collectible=true");
    }

    private Assembly? OnDefaultContextResolving(AssemblyLoadContext context, AssemblyName assemblyName)
    {
      var assemblyNameValue = assemblyName.Name;
      if (assemblyNameValue == null || assemblyLoader == null)
        return null;

      if (AssemblyLoadConstants.PythonAssemblies.Contains(assemblyNameValue, StringComparer.OrdinalIgnoreCase))
      {
        Log.LogTrace("Default resolving python dep name={Name}", assemblyNameValue);
        return assemblyLoader.TryLoad(AssemblyLoadStrategy.RuntimeToDefault, assemblyNameValue);
      }

      return null;
    }

    private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName)
    {
      var assemblyNameOnly = assemblyName.Name ?? string.Empty;
      if (string.IsNullOrEmpty(assemblyNameOnly) || assemblyLoader == null)
        return null;

      if (AssemblyLoadConstants.NonCollectibleAssemblies.Contains(assemblyNameOnly, StringComparer.OrdinalIgnoreCase))
      {
        Log.LogTrace("Resolving NonCollectible name={Name}", assemblyNameOnly);
        return assemblyLoader.TryLoad(AssemblyLoadStrategy.DefaultContext, assemblyNameOnly) ??
               assemblyLoader.TryLoad(AssemblyLoadStrategy.CurrentContext, assemblyNameOnly);
      }

      if (string.Equals(assemblyNameOnly, AssemblyLoadConstants.LoaderContractsAssembly, StringComparison.OrdinalIgnoreCase))
      {
        Log.LogTrace("Resolving prefers existing default for contracts name={Name}", assemblyNameOnly);
        var existingAssembly = assemblyLoader.FindExistingInDefaultContext(assemblyNameOnly);
        if (existingAssembly != null)
          return existingAssembly;
      }

      Log.LogTrace("Resolving in current context name={Name}", assemblyNameOnly);
      return assemblyLoader.TryLoad(AssemblyLoadStrategy.CurrentContext, assemblyNameOnly);
    }

    protected override Assembly? Load(AssemblyName assemblyName) => null;

    /// <summary>
    /// Cleans up resources and unregisters event handlers.
    /// </summary>
    public new void Unload()
    {
      Log.LogDebug("RuntimeLoadContext.Unload called path={Path}", runtimePath);
      if (!disposed)
      {
        AssemblyLoadContext.Default.Resolving -= OnDefaultContextResolving;
        disposed = true;
      }
      base.Unload();
    }
  }
}
