using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Rca.Contracts.Infrastructure;

namespace Rca.Loader.Services
{
    /// <summary>
    /// Named pipe service for communication and reload commands.
    /// </summary>
    public class NamedPipeService : INamedPipeService
    {
        private NamedPipeServerStream pipeServer;
        private CancellationTokenSource cancellationTokenSource;
        private Task serverTask;
        private string currentPipeName;

        /// <summary>
        /// Event raised when a reload command is received.
        /// </summary>
        public event EventHandler<string> ReloadRequested;

        /// <summary>
        /// Event raised when a message is received.
        /// </summary>
        public event EventHandler<string> MessageReceived;

        /// <summary>
        /// Gets whether the server is currently running.
        /// </summary>
        public bool IsServerRunning { get; private set; }

        /// <summary>
        /// Starts the named pipe server.
        /// </summary>
        /// <param name="pipeName">Name of the pipe.</param>
        public void StartServer(string pipeName)
        {
            if (IsServerRunning)
            {
                StopServer();
            }

            currentPipeName = pipeName;
            cancellationTokenSource = new CancellationTokenSource();
            
            serverTask = Task.Run(() => RunServer(cancellationTokenSource.Token));
            IsServerRunning = true;
        }

        /// <summary>
        /// Stops the named pipe server.
        /// </summary>
        public void StopServer()
        {
            if (!IsServerRunning)
                return;

            try
            {
                cancellationTokenSource?.Cancel();
                pipeServer?.Close();
                
                if (serverTask != null && !serverTask.IsCompleted)
                {
                    serverTask.Wait(1000); // Wait up to 1 second
                }
            }
            catch (Exception)
            {
                // Ignore cleanup errors
            }
            finally
            {
                pipeServer?.Dispose();
                pipeServer = null;
                cancellationTokenSource?.Dispose();
                cancellationTokenSource = null;
                serverTask = null;
                IsServerRunning = false;
            }
        }

        /// <summary>
        /// Runs the named pipe server loop.
        /// </summary>
        private async Task RunServer(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Create pipe server
                    pipeServer = new NamedPipeServerStream(
                        currentPipeName,
                        PipeDirection.InOut,
                        1, // Max server instances
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous);

                    // Wait for client connection
                    await pipeServer.WaitForConnectionAsync(cancellationToken);

                    // Handle client communication
                    await HandleClient(pipeServer, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    // Log error and continue
                    OnMessageReceived($"NamedPipe error: {ex.Message}");
                    
                    // Wait a bit before retrying
                    await Task.Delay(1000, cancellationToken);
                }
                finally
                {
                    pipeServer?.Close();
                    pipeServer?.Dispose();
                    pipeServer = null;
                }
            }
        }

        /// <summary>
        /// Handles communication with a connected client.
        /// </summary>
        private async Task HandleClient(NamedPipeServerStream pipe, CancellationToken cancellationToken)
        {
            try
            {
                using (var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true))
                using (var writer = new StreamWriter(pipe, Encoding.UTF8, 1024, true))
                {
                    while (pipe.IsConnected && !cancellationToken.IsCancellationRequested)
                    {
                        // Read message from client
                        var message = await reader.ReadLineAsync();
                        if (string.IsNullOrEmpty(message))
                            break;

                        // Process message
                        var response = ProcessMessage(message);

                        // Send response
                        await writer.WriteLineAsync(response);
                        await writer.FlushAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                OnMessageReceived($"Client communication error: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a received message and returns a response.
        /// </summary>
        private string ProcessMessage(string message)
        {
            try
            {
                OnMessageReceived(message);

                // Parse command
                var parts = message.Split('|');
                var command = parts[0]?.ToUpperInvariant();

                switch (command)
                {
                    case "RELOAD":
                        var assemblyPath = parts.Length > 1 ? parts[1] : null;
                        OnReloadRequested(assemblyPath);
                        return "OK|Reload requested";

                    case "PING":
                        return "OK|Pong";

                    case "STATUS":
                        return "OK|Server running";

                    default:
                        return $"ERROR|Unknown command: {command}";
                }
            }
            catch (Exception ex)
            {
                return $"ERROR|{ex.Message}";
            }
        }

        /// <summary>
        /// Raises the ReloadRequested event.
        /// </summary>
        private void OnReloadRequested(string assemblyPath)
        {
            ReloadRequested?.Invoke(this, assemblyPath);
        }

        /// <summary>
        /// Raises the MessageReceived event.
        /// </summary>
        private void OnMessageReceived(string message)
        {
            MessageReceived?.Invoke(this, message);
        }
    }
}