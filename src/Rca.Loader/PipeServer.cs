using Rca.Loader.Contracts;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Rca.Loader
{
    /// <summary>
    /// Named pipe server for receiving hot reload commands from the build system.
    /// </summary>
    public class PipeServer
    {
        private readonly RuntimeManager runtimeManager;
        private NamedPipeServerStream pipeServer;
        private CancellationTokenSource cancellationTokenSource;
        private Task serverTask;

        public PipeServer(RuntimeManager runtimeManager)
        {
            this.runtimeManager = runtimeManager ?? throw new ArgumentNullException(nameof(runtimeManager));
        }

        /// <summary>
        /// Starts the pipe server.
        /// </summary>
        public void Start()
        {
            if (serverTask != null)
                return; // Already started

            cancellationTokenSource = new CancellationTokenSource();
            serverTask = Task.Run(() => RunServerAsync(cancellationTokenSource.Token));
        }

        /// <summary>
        /// Stops the pipe server.
        /// </summary>
        public void Stop()
        {
            cancellationTokenSource?.Cancel();
            pipeServer?.Close();
            serverTask?.Wait(TimeSpan.FromSeconds(5));
            
            cancellationTokenSource?.Dispose();
            pipeServer?.Dispose();
        }

        private async Task RunServerAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Create new pipe server instance
                        pipeServer = new NamedPipeServerStream(
                            HotReloadConstants.PipeName, 
                            PipeDirection.InOut, 
                            1, 
                            PipeTransmissionMode.Message,
                            PipeOptions.Asynchronous);

                        // Wait for client connection
                        await pipeServer.WaitForConnectionAsync(cancellationToken);

                        // Handle client
                        await HandleClientAsync(pipeServer, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break; // Expected when cancellation is requested
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue serving
                        Console.WriteLine($"[ERROR] Pipe server error: {ex.Message}");
                        await Task.Delay(1000, cancellationToken); // Brief delay before retrying
                    }
                    finally
                    {
                        pipeServer?.Dispose();
                        pipeServer = null;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Fatal pipe server error: {ex.Message}");
            }
        }

        private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
        {
            try
            {
                // Read message
                var buffer = new byte[4096];
                var bytesRead = await pipe.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                
                if (bytesRead == 0)
                    return;

                var json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                var message = JsonConvert.DeserializeObject<CommandMessage>(json);

                // Process command
                await ProcessCommandAsync(message, pipe, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error handling pipe client: {ex.Message}");
                
                // Send error response
                try
                {
                    await SendEventAsync(pipe, "RELOAD_FAIL", new ErrorPayload 
                    { 
                        Message = ex.Message,
                        Exception = ex.ToString()
                    }, cancellationToken);
                }
                catch
                {
                    // Ignore errors when sending error response
                }
            }
        }

        private async Task ProcessCommandAsync(CommandMessage command, NamedPipeServerStream pipe, CancellationToken cancellationToken)
        {
            if (command?.Command == "RELOAD")
            {
                await SendEventAsync(pipe, "RELOAD_ACCEPTED", null, cancellationToken);
                
                try
                {
                    await SendEventAsync(pipe, "RELOAD_START", null, cancellationToken);
                    
                    // Perform reload on UI thread
                    await Task.Run(() => runtimeManager.Reload());
                    
                    await SendEventAsync(pipe, "RELOAD_DONE", null, cancellationToken);
                }
                catch (Exception ex)
                {
                    await SendEventAsync(pipe, "RELOAD_FAIL", new ErrorPayload 
                    { 
                        Message = ex.Message,
                        Exception = ex.ToString()
                    }, cancellationToken);
                }
            }
            else
            {
                await SendEventAsync(pipe, "UNKNOWN_COMMAND", new ErrorPayload 
                { 
                    Message = $"Unknown command: {command?.Command}" 
                }, cancellationToken);
            }
        }

        private async Task SendEventAsync(NamedPipeServerStream pipe, string eventName, object payload, CancellationToken cancellationToken)
        {
            try
            {
                var eventMessage = new EventMessage
                {
                    Event = eventName,
                    Payload = payload
                };

                var json = JsonConvert.SerializeObject(eventMessage);
                var bytes = Encoding.UTF8.GetBytes(json);
                
                await pipe.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
                await pipe.FlushAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to send event {eventName}: {ex.Message}");
            }
        }
    }
}