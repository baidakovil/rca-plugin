using System;
using System.IO.Pipes;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Rca.Loader
{
    /// <summary>
    /// Handles named pipe server communication for RCA Loader.
    /// </summary>
    public class PipeServer : IDisposable
    {
        private readonly string pipeName;
        private CancellationTokenSource? pipeCts;
        private Task? listenTask;

        public delegate Task<PipeResponse> CommandHandler(PipeCommand command);
        private readonly CommandHandler handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="PipeServer"/> class.
        /// </summary>
        /// <param name="pipeName">The name of the pipe.</param>
        /// <param name="handler">The command handler.</param>
        public PipeServer(string pipeName, CommandHandler handler)
        {
            this.pipeName = pipeName;
            this.handler = handler;
            Debug.WriteLine($"DEBUG: PipeServer initialized with pipe name: {pipeName}");
        }

        /// <summary>
        /// Starts the pipe server.
        /// </summary>
        public void Start()
        {
            Debug.WriteLine("DEBUG: Starting pipe server");
            pipeCts = new CancellationTokenSource();
            listenTask = Task.Run(() => ListenLoopAsync(pipeCts.Token));
            Debug.WriteLine("DEBUG: Pipe server listen task started");
        }

        /// <summary>
        /// Stops the pipe server.
        /// </summary>
        public void Stop()
        {
            Debug.WriteLine("DEBUG: Stopping pipe server");
            pipeCts?.Cancel();
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            Debug.WriteLine("DEBUG: Pipe server listen loop started");
            
            while (!token.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                StreamReader? reader = null;
                StreamWriter? writer = null;
                
                try
                {
                    Debug.WriteLine($"DEBUG: Creating new pipe server instance: {pipeName}");
                    
                    // Create the pipe with increased buffer sizes for large test results
                    server = new NamedPipeServerStream(
                        pipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous,
                        65536,  // Input buffer size (64KB)
                        65536); // Output buffer size (64KB)
                    
                    Debug.WriteLine("DEBUG: Waiting for client connection");
                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    
                    Debug.WriteLine("DEBUG: Client connected, creating reader/writer");
                    reader = new StreamReader(server);
                    writer = new StreamWriter(server) { AutoFlush = true };
                    
                    // Process a single command per connection to avoid connection management issues
                    try 
                    {
                        Debug.WriteLine("DEBUG: Reading command from client");
                        var line = await reader.ReadLineAsync().ConfigureAwait(false);
                        
                        if (line == null)
                        {
                            Debug.WriteLine("DEBUG: Client sent null, ending connection");
                        }
                        else
                        {
                            Debug.WriteLine($"DEBUG: Received command: {(line.Length > 100 ? line.Substring(0, 100) + "..." : line)}");
                            var cmd = JsonSerializer.Deserialize<PipeCommand>(line);
                            
                            if (cmd != null)
                            {
                                Debug.WriteLine($"DEBUG: Handling command: {cmd.Command}");
                                var resp = await handler(cmd);
                                
                                if (server.IsConnected)
                                {
                                    var respJson = JsonSerializer.Serialize(resp);
                                    Debug.WriteLine($"DEBUG: Sending response: {(respJson.Length > 100 ? respJson.Substring(0, 100) + "..." : respJson)}");
                                    await writer.WriteLineAsync(respJson).ConfigureAwait(false);
                                    await writer.FlushAsync().ConfigureAwait(false);
                                    
                                    Debug.WriteLine($"DEBUG: Command {cmd.Command} processed successfully");
                                }
                                else
                                {
                                    Debug.WriteLine("DEBUG: Pipe disconnected while processing command");
                                }
                            }
                            else
                            {
                                Debug.WriteLine("DEBUG: Failed to deserialize command");
                            }
                        }
                    }
                    catch (IOException ioEx)
                    {
                        Debug.WriteLine($"DEBUG: IO error in connection: {ioEx.Message}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"DEBUG: Error processing command: {ex.Message}");
                        
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
                            Debug.WriteLine("DEBUG: Failed to send error response");
                        }
                    }
                    
                    Debug.WriteLine("DEBUG: Command processing complete, closing connection");
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("DEBUG: Pipe server operation canceled");
                    break;
                }
                catch (Exception ex)
                {
                    // Log the error
                    Debug.WriteLine($"DEBUG: Error in pipe server: {ex.Message}");
                }
                finally
                {
                    // Clean up resources in proper order - critical for avoiding pipe errors
                    try
                    {
                        writer?.Close();
                        writer?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"DEBUG: Error disposing writer: {ex.Message}");
                    }
                    
                    try
                    {
                        reader?.Close();
                        reader?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"DEBUG: Error disposing reader: {ex.Message}");
                    }
                    
                    try
                    {
                        if (server != null)
                        {
                            if (server.IsConnected)
                            {
                                server.Disconnect();
                            }
                            server.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"DEBUG: Error disposing server: {ex.Message}");
                    }
                }
                
                // Brief delay before creating a new pipe instance
                if (!token.IsCancellationRequested)
                {
                    await Task.Delay(100, token).ConfigureAwait(false);
                }
            }
            
            Debug.WriteLine("DEBUG: Pipe server listen loop ended");
        }

        /// <summary>
        /// Disposes resources used by the pipe server.
        /// </summary>
        public void Dispose()
        {
            Debug.WriteLine("DEBUG: Disposing pipe server");
            Stop();
        }
    }

    public record PipeCommand(string Command, string? Payload);
    public record PipeResponse { public string Status { get; set; } = string.Empty; public string Message { get; set; } = string.Empty; }
}
