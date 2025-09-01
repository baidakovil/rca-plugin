using Newtonsoft.Json;
using Rca.Loader.Contracts;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rca.Loader
{
    /// <summary>
    /// Named pipe server for hot reload communication.
    /// </summary>
    public class PipeServer
    {
        private readonly RuntimeManager runtimeManager;
        private CancellationTokenSource cancellationTokenSource;
        private Task serverTask;

        /// <summary>
        /// Initializes a new instance of the PipeServer class.
        /// </summary>
        /// <param name="runtimeManager">The runtime manager</param>
        public PipeServer(RuntimeManager runtimeManager)
        {
            this.runtimeManager = runtimeManager ?? throw new ArgumentNullException(nameof(runtimeManager));
            
            // Subscribe to runtime events
            this.runtimeManager.ReloadStarted += OnReloadStarted;
            this.runtimeManager.ReloadCompleted += OnReloadCompleted;
            this.runtimeManager.ReloadFailed += OnReloadFailed;
        }

        /// <summary>
        /// Starts the pipe server.
        /// </summary>
        public void Start()
        {
            if (serverTask != null)
                return;

            cancellationTokenSource = new CancellationTokenSource();
            serverTask = Task.Run(RunServerAsync, cancellationTokenSource.Token);
        }

        /// <summary>
        /// Stops the pipe server.
        /// </summary>
        public void Stop()
        {
            cancellationTokenSource?.Cancel();
            try
            {
                serverTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (OperationCanceledException)
            {
                // Expected when canceling
            }
            cancellationTokenSource?.Dispose();
            serverTask?.Dispose();
            serverTask = null;
        }

        /// <summary>
        /// Runs the pipe server loop.
        /// </summary>
        private async Task RunServerAsync()
        {
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    using (var pipeServer = new NamedPipeServerStream(
                        HotReloadConstants.PipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous))
                    {
                        LogMessage("Waiting for pipe client connection...");
                        
                        await pipeServer.WaitForConnectionAsync(cancellationTokenSource.Token);
                        LogMessage("Pipe client connected");

                        await HandleClientAsync(pipeServer);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Server is shutting down
                    break;
                }
                catch (Exception ex)
                {
                    LogMessage($"Pipe server error: {ex.Message}");
                    
                    // Wait before retrying
                    try
                    {
                        await Task.Delay(1000, cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Handles a connected pipe client.
        /// </summary>
        private async Task HandleClientAsync(NamedPipeServerStream pipeServer)
        {
            try
            {
                using (var reader = new StreamReader(pipeServer, Encoding.UTF8, false, 1024, true))
                using (var writer = new StreamWriter(pipeServer, Encoding.UTF8, 1024, true) { AutoFlush = true })
                {
                    while (pipeServer.IsConnected && !cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        var message = await reader.ReadLineAsync();
                        if (message == null)
                            break;

                        await ProcessMessageAsync(message, writer);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error handling pipe client: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes an incoming pipe message.
        /// </summary>
        private async Task ProcessMessageAsync(string messageJson, StreamWriter writer)
        {
            try
            {
                var message = JsonConvert.DeserializeObject<PipeMessage>(messageJson);
                
                switch (message.Type?.ToUpperInvariant())
                {
                    case "COMMAND":
                        await ProcessCommandAsync(message, writer);
                        break;
                    default:
                        LogMessage($"Unknown message type: {message.Type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error processing message: {ex.Message}");
                await SendEventAsync(writer, "PROCESS_ERROR", ex.Message);
            }
        }

        /// <summary>
        /// Processes a command message.
        /// </summary>
        private async Task ProcessCommandAsync(PipeMessage message, StreamWriter writer)
        {
            try
            {
                var commandMessage = JsonConvert.DeserializeObject<CommandMessage>(message.Payload ?? "{}");
                
                switch (commandMessage.Command?.ToUpperInvariant())
                {
                    case "RELOAD":
                        await ProcessReloadCommandAsync(message.Payload, writer);
                        break;
                    default:
                        LogMessage($"Unknown command: {commandMessage.Command}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error processing command: {ex.Message}");
                await SendEventAsync(writer, "COMMAND_ERROR", ex.Message);
            }
        }

        /// <summary>
        /// Processes a reload command.
        /// </summary>
        private async Task ProcessReloadCommandAsync(string payloadJson, StreamWriter writer)
        {
            try
            {
                await SendEventAsync(writer, "RELOAD_ACCEPTED", "Reload command accepted");

                ReloadPayload payload = null;
                if (!string.IsNullOrEmpty(payloadJson))
                {
                    payload = JsonConvert.DeserializeObject<ReloadPayload>(payloadJson);
                }

                // Execute reload on UI thread (since we're dealing with Revit)
                var folder = payload?.Folder;
                var force = payload?.Force ?? false;

                // For now, execute synchronously. In a real implementation,
                // you might want to marshal this to the UI thread.
                runtimeManager.Reload(folder, force);
            }
            catch (Exception ex)
            {
                LogMessage($"Error during reload: {ex.Message}");
                await SendEventAsync(writer, "RELOAD_FAIL", ex.Message);
            }
        }

        /// <summary>
        /// Sends an event message to the client.
        /// </summary>
        private async Task SendEventAsync(StreamWriter writer, string eventName, string payload)
        {
            try
            {
                var eventMessage = new EventMessage
                {
                    Type = "EVENT",
                    Event = eventName,
                    Payload = payload
                };

                var json = JsonConvert.SerializeObject(eventMessage);
                await writer.WriteLineAsync(json);
            }
            catch (Exception ex)
            {
                LogMessage($"Error sending event: {ex.Message}");
            }
        }

        /// <summary>
        /// Event handler for reload started.
        /// </summary>
        private void OnReloadStarted(object sender, string folder)
        {
            LogMessage($"Reload started for folder: {folder}");
        }

        /// <summary>
        /// Event handler for reload completed.
        /// </summary>
        private void OnReloadCompleted(object sender, string folder)
        {
            LogMessage($"Reload completed for folder: {folder}");
        }

        /// <summary>
        /// Event handler for reload failed.
        /// </summary>
        private void OnReloadFailed(object sender, string error)
        {
            LogMessage($"Reload failed: {error}");
        }

        /// <summary>
        /// Logs a message.
        /// </summary>
        private void LogMessage(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[PipeServer] {message}");
            // TODO: Integrate with existing logging system if available
        }
    }
}