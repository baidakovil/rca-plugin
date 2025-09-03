using System;
using System.Threading;
using System.Threading.Tasks;

namespace Rca.Contracts.Services
{
    /// <summary>
    /// Service for managing named pipe communication for hot-reload functionality.
    /// </summary>
    public interface IPipeServer
    {
        /// <summary>
        /// Starts the pipe server to listen for reload commands.
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Stops the pipe server.
        /// </summary>
        void Stop();

        /// <summary>
        /// Event raised when a reload command is received.
        /// </summary>
        event EventHandler<string> ReloadRequested;

        /// <summary>
        /// Event raised when a status request is received.
        /// </summary>
        event EventHandler StatusRequested;
    }
}