using System;
using System.IO.Pipes;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Rca.Loader.Contracts;

namespace Rca.Loader.Services
{
    /// <summary>
    /// Handles named pipe server communication for RCA Loader.
    /// </summary>
    public class PipeServerService : IPipeServerService, IDisposable
    {
        private readonly string pipeName;
        private CancellationTokenSource? pipeCts;
        private Task? listenTask;

        public delegate Task<PipeResponse> CommandHandler(PipeCommand command);
        private readonly CommandHandler handler;

        /// <summary>
        /// Gets whether the pipe server is currently running.
        /// </summary>
        public bool IsRunning => pipeCts != null && !pipeCts.Token.IsCancellationRequested;

        /// <summary>
        /// Initializes a new instance of the <see cref="PipeServerService"/> class.
        /// </summary>
        /// <param name="pipeName">The name of the pipe.</param>
        /// <param name="handler">The command handler.</param>
        public PipeServerService(string pipeName, CommandHandler handler)
        {
            this.pipeName = pipeName;
            this.handler = handler;
        }

        /// <summary>
        /// Starts the pipe server.
        /// </summary>
        public void Start()
        {
            pipeCts = new CancellationTokenSource();
            listenTask = Task.Run(() => ListenLoopAsync(pipeCts.Token));
        }

        /// <summary>
        /// Stops the pipe server.
        /// </summary>
        public void Stop()
        {
            pipeCts?.Cancel();
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                StreamReader? reader = null;
                StreamWriter? writer = null;
                
                try
                {
                    // Create the pipe with increased buffer sizes for large test results
                    server = new NamedPipeServerStream(
                        pipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous,
                        65536,  // Input buffer size (64KB)
                        65536); // Output buffer size (64KB)
                    
                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    
                    reader = new StreamReader(server);
                    writer = new StreamWriter(server) { AutoFlush = true };
                    
                    // Process a single command per connection to avoid connection management issues
                    try 
                    {
                        var line = await reader.ReadLineAsync().ConfigureAwait(false);
                        
                        if (line != null)
                        {
                            var cmd = JsonSerializer.Deserialize<PipeCommand>(line);
                            
                            if (cmd != null)
                            {
                                var resp = await handler(cmd);
                                
                                if (server.IsConnected)
                                {
                                    var respJson = JsonSerializer.Serialize(resp);
                                    await writer.WriteLineAsync(respJson).ConfigureAwait(false);
                                    await writer.FlushAsync().ConfigureAwait(false);
                                }
                            }
                        }
                    }
                    catch (IOException)
                    {
                        // Connection closed by client
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            if (server.IsConnected && writer != null)
                            {
                                // Try to send an error response
                                var errorResp = new PipeResponse { 
                                    Status = "ERROR", 
                                    Message = $"Server error: {ex.Message}" 
                                };
                                var errorJson = JsonSerializer.Serialize(errorResp);
                                await writer.WriteLineAsync(errorJson).ConfigureAwait(false);
                                await writer.FlushAsync().ConfigureAwait(false);
                            }
                        }
                        catch 
                        {
                            // Ignore errors when sending error response
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in pipe server: {ex.Message}");
                }
                finally
                {
                    // Clean up resources in proper order
                    try { writer?.Close(); writer?.Dispose(); } catch { }
                    try { reader?.Close(); reader?.Dispose(); } catch { }
                    try 
                    { 
                        if (server?.IsConnected == true) server.Disconnect();
                        server?.Dispose(); 
                    } 
                    catch { }
                }
                
                // Brief delay before creating a new pipe instance
                if (!token.IsCancellationRequested)
                {
                    await Task.Delay(100, token).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Disposes resources used by the pipe server.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }
    }

    /// <summary>
    /// Represents a command sent through the pipe.
    /// </summary>
    /// <param name="Command">The command name.</param>
    /// <param name="Payload">The command payload.</param>
    public record PipeCommand(string Command, string? Payload);
    
    /// <summary>
    /// Represents a response sent through the pipe.
    /// </summary>
    public record PipeResponse 
    { 
        /// <summary>
        /// Gets or sets the response status.
        /// </summary>
        public string Status { get; set; } = string.Empty; 
        
        /// <summary>
        /// Gets or sets the response message.
        /// </summary>
        public string Message { get; set; } = string.Empty; 
    }
}