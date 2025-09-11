using System;
using System.Threading.Tasks;

namespace Rca.Loader.Contracts
{
    /// <summary>
    /// Interface for pipe server communication service.
    /// </summary>
    public interface IPipeServerService
    {
        /// <summary>
        /// Gets whether the pipe server is currently running.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Starts the pipe server.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the pipe server.
        /// </summary>
        void Stop();
    }
}