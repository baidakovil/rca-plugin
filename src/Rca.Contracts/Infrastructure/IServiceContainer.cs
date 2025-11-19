using System;

namespace Rca.Contracts.Infrastructure
{
  /// <summary>
  /// Interface for service registration operations.
  /// </summary>
  public interface IServiceRegistrar
  {
    /// <summary>
    /// Registers a service instance.
    /// </summary>
    void Register<TInterface>(TInterface implementation) where TInterface : class;

    /// <summary>
    /// Registers a service factory.
    /// </summary>
    void Register<TInterface>(Func<TInterface> factory) where TInterface : class;
  }

  /// <summary>
  /// Interface for service resolution operations.
  /// </summary>
  public interface IServiceResolver
  {
    /// <summary>
    /// Resolves a service instance.
    /// </summary>
    TInterface Resolve<TInterface>() where TInterface : class;

    /// <summary>
    /// Checks if a service is registered.
    /// </summary>
    bool IsRegistered<TInterface>() where TInterface : class;
  }

  /// <summary>
  /// Combined interface for dependency injection container.
  /// </summary>
  public interface IServiceContainer : IServiceRegistrar, IServiceResolver
  {
  }
}
