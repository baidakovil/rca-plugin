using Rca.Loader.Contracts;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Rca.Loader
{
    /// <summary>
    /// Manages named pipe communication for hot reload commands.
    /// </summary>
    internal class PipeServer : IDisposable
    {
        private readonly RuntimeManager runtimeManager;
        private readonly CancellationTokenSource cancellationTokenSource;
        private Task serverTask;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PipeServer"/> class.
        /// </summary>
        /// <param name="runtimeManager">The runtime manager.</param>
        public PipeServer(RuntimeManager runtimeManager)
        {
            this.runtimeManager = runtimeManager ?? throw new ArgumentNullException(nameof(runtimeManager));
            cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Starts the pipe server.
        /// </summary>
        public void Start()
        {
            if (serverTask != null)
                return;

            serverTask = Task.Run(ServerLoop, cancellationTokenSource.Token);
        }

        /// <summary>
        /// Stops the pipe server.
        /// </summary>
        public void Stop()
        {
            cancellationTokenSource?.Cancel();
            serverTask?.Wait(TimeSpan.FromSeconds(5));
        }

        /// <summary>
        /// The main server loop that handles pipe connections.
        /// </summary>
        private async Task ServerLoop()
        {
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(
                        HotReloadConstants.PipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous);

                    await pipe.WaitForConnectionAsync(cancellationTokenSource.Token);

                    _ = Task.Run(async () => await HandleClientAsync(pipe), cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Pipe server error: {ex.Message}");
                    await Task.Delay(1000, cancellationTokenSource.Token);
                }
            }
        }

        /// <summary>
        /// Handles a connected client.
        /// </summary>
        /// <param name="pipe">The connected pipe.</param>
        private async Task HandleClientAsync(NamedPipeServerStream pipe)
        {
            try
            {
                var buffer = new byte[4096];
                var bytesRead = await pipe.ReadAsync(buffer, 0, buffer.Length, cancellationTokenSource.Token);

                if (bytesRead > 0)
                {
                    var messageJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    await ProcessMessageAsync(pipe, messageJson);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling pipe client: {ex.Message}");
                await SendEventAsync(pipe, MessageTypes.RuntimeError, new ErrorPayload
                {
                    Message = "Error handling client request",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Processes an incoming message.
        /// </summary>
        /// <param name="pipe">The pipe to send responses to.</param>
        /// <param name="messageJson">The incoming message JSON.</param>
        private async Task ProcessMessageAsync(NamedPipeServerStream pipe, string messageJson)
        {
            try
            {
                var message = JsonSerializer.Deserialize<CommandMessage>(messageJson);

                if (message.Type == MessageTypes.Reload)
                {
                    await SendEventAsync(pipe, MessageTypes.ReloadAccepted, null);
                    await HandleReloadCommandAsync(pipe, message.Payload);
                }
                else
                {
                    await SendEventAsync(pipe, MessageTypes.RuntimeError, new ErrorPayload
                    {
                        Message = $"Unknown command type: {message.Type}"
                    });
                }
            }
            catch (Exception ex)
            {
                await SendEventAsync(pipe, MessageTypes.RuntimeError, new ErrorPayload
                {
                    Message = "Error processing message",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Handles a reload command.
        /// </summary>
        /// <param name="pipe">The pipe to send responses to.</param>
        /// <param name="payload">The command payload.</param>
        private async Task HandleReloadCommandAsync(NamedPipeServerStream pipe, object payload)
        {
            try
            {
                await SendEventAsync(pipe, MessageTypes.ReloadStart, null);

                string folderOverride = null;
                if (payload is JsonElement element && element.ValueKind == JsonValueKind.Object)
                {
                    if (element.TryGetProperty("folder", out var folderProp))
                    {
                        folderOverride = folderProp.GetString();
                    }
                }

                runtimeManager.Reload(folderOverride, force: true);

                await SendEventAsync(pipe, MessageTypes.ReloadDone, null);
            }
            catch (Exception ex)
            {
                await SendEventAsync(pipe, MessageTypes.ReloadFail, new ErrorPayload
                {
                    Message = "Reload failed",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Sends an event message to the client.
        /// </summary>
        /// <param name="pipe">The pipe to send the message to.</param>
        /// <param name="eventType">The event type.</param>
        /// <param name="payload">The event payload.</param>
        private async Task SendEventAsync(NamedPipeServerStream pipe, string eventType, object payload)
        {
            try
            {
                var eventMessage = new EventMessage
                {
                    Type = eventType,
                    Payload = payload,
                    Timestamp = DateTime.UtcNow.ToString("O")
                };

                var json = JsonSerializer.Serialize(eventMessage);
                var bytes = Encoding.UTF8.GetBytes(json);

                await pipe.WriteAsync(bytes, 0, bytes.Length, cancellationTokenSource.Token);
                await pipe.FlushAsync(cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending event: {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes the pipe server.
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                Stop();
                cancellationTokenSource?.Dispose();
                disposed = true;
            }
        }
    }
}