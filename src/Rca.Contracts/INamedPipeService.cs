using System;

namespace Rca.Contracts.Infrastructure
{
    /// <summary>
    /// Interface for named pipe communication service.
    /// </summary>
    public interface INamedPipeService
    {
        /// <summary>
        /// Starts the named pipe server.
        /// </summary>
        /// <param name="pipeName">Name of the pipe.</param>
        void StartServer(string pipeName);

        /// <summary>
        /// Stops the named pipe server.
        /// </summary>
        void StopServer();

        /// <summary>
        /// Event raised when a reload command is received.
        /// </summary>
        event EventHandler<string> ReloadRequested;

        /// <summary>
        /// Event raised when a message is received.
        /// </summary>
        event EventHandler<string> MessageReceived;

        /// <summary>
        /// Gets whether the server is currently running.
        /// </summary>
        bool IsServerRunning { get; }
    }
}