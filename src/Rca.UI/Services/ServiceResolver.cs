using Rca.Contracts;
using Rca.Contracts.Infrastructure;
using System;
using Microsoft.Extensions.Logging;
using Rca.UI.Logging;

namespace Rca.UI.Services
{
  /// <summary>
  /// Service for resolving dependencies with graceful fallbacks to default implementations.
  /// </summary>
  public class ServiceResolver
  {
    private static readonly ILogger Log = UiLog.GetLogger<ServiceResolver>();
    private readonly ServiceContainer container;

    /// <summary>
    /// Initializes a new instance of the ServiceResolver class.
    /// </summary>
    /// <param name="container">The service container to resolve from.</param>
    public ServiceResolver(ServiceContainer container)
    {
      this.container = container ?? throw new ArgumentNullException(nameof(container));
    }

    /// <summary>
    /// Resolves a service from the container or returns a default implementation.
    /// </summary>
    /// <typeparam name="T">The service interface type.</typeparam>
    /// <returns>The resolved service or a default implementation, which may be null.</returns>
    public T? ResolveOrDefault<T>() where T : class
    {
      try
      {
        if (container.IsRegistered<T>())
        {
          return container.Resolve<T>();
        }

        LogServiceNotRegistered<T>();
        return CreateDefaultService<T>();
      }
      catch (InvalidOperationException ex)
      {
        LogServiceResolutionError<T>(ex);
        return CreateDefaultService<T>();
      }
    }

    /// <summary>
    /// Creates a default implementation for the specified service type.
    /// </summary>
    /// <typeparam name="T">The service interface type.</typeparam>
    /// <returns>A default implementation or null if no default is available.</returns>
    private static T? CreateDefaultService<T>() where T : class
    {
      return typeof(T) switch
      {
        var t when t == typeof(IPythonExecutionService) => DefaultServices.CreatePythonExecutionService() as T,
        var t when t == typeof(IRevitContext) => DefaultServices.CreateRevitContext() as T,
        _ => null
      };
    }

    /// <summary>
    /// Logs that a service is not registered.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    private static void LogServiceNotRegistered<T>()
    {
      Log.LogWarning("Service {Service} not registered - using default", typeof(T).Name);
    }

    /// <summary>
    /// Logs a service resolution error.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="ex">The exception that occurred.</param>
    private static void LogServiceResolutionError<T>(Exception ex)
    {
      Log.LogError(ex, "Error resolving service {Service}", typeof(T).Name);
    }
  }
}
