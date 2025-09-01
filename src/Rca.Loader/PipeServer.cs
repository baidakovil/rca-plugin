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
    /// Named pipe server for communicating with build system.
    /// </summary>
    public class PipeServer
    {
        private readonly RuntimeManager runtimeManager;
        private NamedPipeServerStream pipeServer;
        private CancellationTokenSource cancellationTokenSource;
        private Task serverTask;

        /// <summary>
        /// Initializes a new instance of the PipeServer class.
        /// </summary>
        /// <param name="runtimeManager">The runtime manager instance</param>
        public PipeServer(RuntimeManager runtimeManager)
        {
            this.runtimeManager = runtimeManager ?? throw new ArgumentNullException(nameof(runtimeManager));
        }

        /// <summary>
        /// Starts the pipe server.
        /// </summary>
        public void Start()
        {
            cancellationTokenSource = new CancellationTokenSource();
            serverTask = Task.Run(RunServerLoop, cancellationTokenSource.Token);
        }

        /// <summary>
        /// Stops the pipe server.
        /// </summary>
        public void Stop()
        {
            cancellationTokenSource?.Cancel();
            pipeServer?.Close();
            serverTask?.Wait(TimeSpan.FromSeconds(5));
        }

        private async Task RunServerLoop()
        {
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    pipeServer = new NamedPipeServerStream(
                        PipeConstants.PipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Message);

                    await pipeServer.WaitForConnectionAsync(cancellationTokenSource.Token);
                    await HandleClient(pipeServer);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    SendEvent("RUNTIME_ERROR", new { error = ex.Message, source = "PipeServer" });
                    await Task.Delay(1000, cancellationTokenSource.Token);
                }
                finally
                {
                    pipeServer?.Close();
                    pipeServer = null;
                }
            }
        }

        private async Task HandleClient(NamedPipeServerStream pipe)
        {
            var buffer = new byte[4096];
            var sb = new StringBuilder();

            while (pipe.IsConnected && !cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var bytesRead = await pipe.ReadAsync(buffer, 0, buffer.Length, cancellationTokenSource.Token);
                    if (bytesRead == 0)
                        break;

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));

                    // Process complete messages (assume newline-delimited JSON)
                    var content = sb.ToString();
                    var lines = content.Split('\n');
                    
                    for (int i = 0; i < lines.Length - 1; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(lines[i]))
                        {
                            await ProcessMessage(lines[i], pipe);
                        }
                    }

                    // Keep incomplete last line
                    sb.Clear();
                    if (!string.IsNullOrWhiteSpace(lines[lines.Length - 1]))
                    {
                        sb.Append(lines[lines.Length - 1]);
                    }
                }
                catch (Exception ex)
                {
                    SendEvent("RUNTIME_ERROR", new { error = ex.Message, source = "PipeClient" });
                    break;
                }
            }
        }

        private async Task ProcessMessage(string message, NamedPipeServerStream pipe)
        {
            try
            {
                var command = JsonSerializer.Deserialize<CommandMessage>(message);
                
                switch (command.Command?.ToUpperInvariant())
                {
                    case "RELOAD":
                        await ProcessReloadCommand(command, pipe);
                        break;
                    default:
                        await SendResponse(pipe, new EventMessage 
                        { 
                            Type = "EVENT",
                            Event = "UNKNOWN_COMMAND", 
                            Data = new { command = command.Command } 
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                await SendResponse(pipe, new ErrorMessage
                {
                    Type = "ERROR",
                    Message = "Failed to process message",
                    Exception = ex.ToString()
                });
            }
        }

        private async Task ProcessReloadCommand(CommandMessage command, NamedPipeServerStream pipe)
        {
            await SendResponse(pipe, new EventMessage 
            { 
                Type = "EVENT",
                Event = "RELOAD_ACCEPTED", 
                Data = new { timestamp = DateTime.UtcNow } 
            });

            try
            {
                var payloadJson = command.Payload?.ToString();
                ReloadPayload payload = null;
                if (!string.IsNullOrEmpty(payloadJson))
                {
                    payload = JsonSerializer.Deserialize<ReloadPayload>(payloadJson);
                }
                runtimeManager.Reload(payload?.Folder, payload?.Force ?? false);
            }
            catch (Exception ex)
            {
                await SendResponse(pipe, new ErrorMessage
                {
                    Type = "ERROR",
                    Message = "Reload failed",
                    Exception = ex.ToString()
                });
            }
        }

        private async Task SendResponse(NamedPipeServerStream pipe, object response)
        {
            try
            {
                var json = JsonSerializer.Serialize(response) + "\n";
                var bytes = Encoding.UTF8.GetBytes(json);
                await pipe.WriteAsync(bytes, 0, bytes.Length);
                await pipe.FlushAsync();
            }
            catch (Exception ex)
            {
                SendEvent("RUNTIME_ERROR", new { error = ex.Message, source = "PipeResponse" });
            }
        }

        private void SendEvent(string eventName, object data)
        {
            // For debugging purposes
#if DEBUG
            Console.WriteLine($"[PipeServer] {eventName}: {JsonSerializer.Serialize(data)}");
#endif
        }
    }
}