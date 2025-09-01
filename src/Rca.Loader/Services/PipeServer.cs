using Rca.Loader.Contracts;
using Rca.Loader.Contracts.Protocol;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Rca.Loader.Services
{
    /// <summary>
    /// Named pipe server for receiving hot reload commands from the build system.
    /// Handles bi-directional communication using JSON messages.
    /// </summary>
    public class PipeServer
    {
        private readonly RuntimeManager runtimeManager;
        private NamedPipeServerStream pipeServer;
        private CancellationTokenSource cancellationTokenSource;
        private Task serverTask;

        /// <summary>
        /// Initializes a new instance of the PipeServer.
        /// </summary>
        /// <param name="runtimeManager">The runtime manager to notify of reload commands</param>
        public PipeServer(RuntimeManager runtimeManager)
        {
            this.runtimeManager = runtimeManager ?? throw new ArgumentNullException(nameof(runtimeManager));
        }

        /// <summary>
        /// Starts the pipe server to listen for incoming commands.
        /// </summary>
        public void Start()
        {
            if (serverTask != null && !serverTask.IsCompleted)
            {
                return; // Already running
            }

            cancellationTokenSource = new CancellationTokenSource();
            serverTask = Task.Run(() => RunServerAsync(cancellationTokenSource.Token));
            LogInfo("Pipe server started for hot reload communication.");
        }

        /// <summary>
        /// Stops the pipe server.
        /// </summary>
        public void Stop()
        {
            try
            {
                cancellationTokenSource?.Cancel();
                pipeServer?.Dispose();
                serverTask?.Wait(TimeSpan.FromSeconds(5));
                LogInfo("Pipe server stopped.");
            }
            catch (Exception ex)
            {
                LogError($"Error stopping pipe server: {ex.Message}");
            }
        }

        /// <summary>
        /// Main server loop that handles pipe connections.
        /// </summary>
        private async Task RunServerAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    pipeServer = new NamedPipeServerStream(
                        HotReloadConstants.PipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous);

                    LogInfo($"Waiting for pipe connection on '{HotReloadConstants.PipeName}'...");
                    
                    await pipeServer.WaitForConnectionAsync(cancellationToken);
                    LogInfo("Pipe client connected.");

                    await HandleClientAsync(pipeServer, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogError($"Pipe server error: {ex.Message}");
                    await Task.Delay(1000, cancellationToken);
                }
                finally
                {
                    pipeServer?.Dispose();
                    pipeServer = null;
                }
            }
        }

        /// <summary>
        /// Handles communication with a connected pipe client.
        /// </summary>
        private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
        {
            try
            {
                using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
                using var writer = new StreamWriter(pipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                string messageJson;
                while (!cancellationToken.IsCancellationRequested && 
                       (messageJson = await reader.ReadLineAsync()) != null)
                {
                    await ProcessMessageAsync(messageJson, writer);
                }
            }
            catch (Exception ex)
            {
                LogError($"Error handling pipe client: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes incoming JSON messages and sends appropriate responses.
        /// </summary>
        private async Task ProcessMessageAsync(string messageJson, StreamWriter writer)
        {
            try
            {
                var command = JsonSerializer.Deserialize<CommandMessage>(messageJson);
                
                if (command?.Type == MessageTypes.Reload)
                {
                    await SendEventAsync(writer, MessageTypes.ReloadAccepted, null);
                    await SendEventAsync(writer, MessageTypes.ReloadStart, null);

                    var success = runtimeManager.Reload();

                    if (success)
                    {
                        await SendEventAsync(writer, MessageTypes.ReloadDone, null);
                        LogInfo("Runtime reload completed successfully.");
                    }
                    else
                    {
                        await SendEventAsync(writer, MessageTypes.ReloadFail, new ErrorPayload 
                        { 
                            Message = "Failed to reload runtime. Check logs for details." 
                        });
                        LogError("Runtime reload failed.");
                    }
                }
                else
                {
                    LogWarning($"Unknown command type: {command?.Type}");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error processing message: {ex.Message}");
                await SendEventAsync(writer, MessageTypes.RuntimeError, new ErrorPayload 
                { 
                    Message = "Error processing reload command",
                    Exception = ex.ToString()
                });
            }
        }

        /// <summary>
        /// Sends an event message to the pipe client.
        /// </summary>
        private async Task SendEventAsync(StreamWriter writer, string eventType, object payload)
        {
            try
            {
                var eventMessage = new EventMessage
                {
                    Type = eventType,
                    Payload = payload,
                    Timestamp = DateTime.Now
                };

                var json = JsonSerializer.Serialize(eventMessage);
                await writer.WriteLineAsync(json);
                LogInfo($"Sent event: {eventType}");
            }
            catch (Exception ex)
            {
                LogError($"Error sending event {eventType}: {ex.Message}");
            }
        }

        private void LogInfo(string message)
        {
            Console.WriteLine($"[RCA Pipe] {message}");
        }

        private void LogWarning(string message)
        {
            Console.WriteLine($"[RCA Pipe] WARNING: {message}");
        }

        private void LogError(string message)
        {
            Console.WriteLine($"[RCA Pipe] ERROR: {message}");
        }
    }
}