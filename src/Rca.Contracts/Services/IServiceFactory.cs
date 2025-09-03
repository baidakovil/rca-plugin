namespace Rca.Contracts.Services
{
    /// <summary>
    /// Factory for creating service instances with proper dependency injection.
    /// </summary>
    public interface IServiceFactory
    {
        /// <summary>
        /// Creates an instance of the specified service type.
        /// </summary>
        T Create<T>() where T : class;

        /// <summary>
        /// Checks if a service type can be created.
        /// </summary>
        bool CanCreate<T>() where T : class;
    }
}