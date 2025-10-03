using System;
using System.IO.Pipes;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Rca.Loader.Contracts;
using Rca.Loader.Logging; // unified logging
using Microsoft.Extensions.Logging;

namespace Rca.Loader.Services
{
    /// <summary>
    /// Handles named pipe server communication for RCA Loader.
    /// Uses unified logging (LoaderLog) instead of Debug.WriteLine.
    /// Each connection processes exactly one command then disconnects to simplify lifecycle.
    /// </summary>
    public class PipeServerService : IPipeServerService, IDisposable
    {
        private readonly string pipeName;
        private CancellationTokenSource? pipeCts;
        private Task? listenTask;
        private readonly ILogger _log = LoaderLog.GetLogger<PipeServerService>();

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
            this.pipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// Starts the pipe server (idempotent).
        /// </summary>
        public void Start()
        {
            if (IsRunning) return;
            pipeCts = new CancellationTokenSource();
            _log.LogInformation("Pipe server starting on {Pipe}", pipeName);
            listenTask = Task.Run(() => ListenLoopAsync(pipeCts.Token));
        }

        /// <summary>
        /// Requests server stop; active connection finishes gracefully.
        /// </summary>
        public void Stop()
        {
            if (pipeCts == null) return;
            _log.LogInformation("Pipe server stopping (pipe={Pipe})", pipeName);
            try { pipeCts.Cancel(); } catch { }
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
                    server = new NamedPipeServerStream(
                        pipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous,
                        65536,
                        65536);

                    _log.LogDebug("Waiting for connection (pipe={Pipe})", pipeName);
                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    _log.LogInformation("Client connected (pipe={Pipe})", pipeName);

                    reader = new StreamReader(server);
                    writer = new StreamWriter(server) { AutoFlush = true };

                    string? line = null;
                    try
                    {
                        line = await reader.ReadLineAsync().ConfigureAwait(false);
                        if (line == null)
                        {
                            _log.LogDebug("Empty connection (no data) pipe={Pipe}", pipeName);
                            continue;
                        }

                        PipeCommand? cmd = null;
                        try
                        {
                            cmd = JsonSerializer.Deserialize<PipeCommand>(line);
                        }
                        catch (JsonException jex)
                        {
                            _log.LogWarning(jex, "Failed to deserialize command jsonLength={Len}", line.Length);
                        }

                        if (cmd == null)
                        {
                            await TryWriteErrorAsync(writer, server, "INVALID_COMMAND", "Deserialization failed").ConfigureAwait(false);
                            continue;
                        }

                        _log.LogInformation("Received command {Command} (payloadLen={Len})", cmd.Command, cmd.Payload?.Length ?? 0);
                        PipeResponse resp;
                        try
                        {
                            resp = await handler(cmd).ConfigureAwait(false);
                        }
                        catch (Exception hex)
                        {
                            _log.LogError(hex, "Command handler threw (command={Command})", cmd.Command);
                            resp = new PipeResponse { Status = "ERROR", Message = hex.Message };
                        }

                        if (server.IsConnected)
                        {
                            var json = JsonSerializer.Serialize(resp);
                            await writer.WriteLineAsync(json).ConfigureAwait(false);
                        }
                    }
                    catch (IOException)
                    {
                        _log.LogDebug("Client disconnected mid-communication pipe={Pipe}", pipeName);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception exCmd)
                    {
                        _log.LogError(exCmd, "Unhandled exception processing command rawLength={Len}", line?.Length ?? 0);
                        await TryWriteErrorAsync(writer, server, "SERVER_ERROR", exCmd.Message).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exLoop)
                {
                    _log.LogError(exLoop, "Error in pipe server outer loop");
                }
                finally
                {
                    try { writer?.Dispose(); } catch { }
                    try { reader?.Dispose(); } catch { }
                    try { if (server?.IsConnected == true) server.Disconnect(); server?.Dispose(); } catch { }
                }

                if (!token.IsCancellationRequested)
                {
                    // small delay to avoid tight loop on repeated failures
                    await Task.Delay(100, token).ConfigureAwait(false);
                }
            }
            _log.LogInformation("Pipe server listener loop exiting (pipe={Pipe})", pipeName);
        }

        private static async Task TryWriteErrorAsync(StreamWriter? writer, NamedPipeServerStream? server, string status, string message)
        {
            if (writer == null || server == null || !server.IsConnected) return;
            try
            {
                var errorResp = new PipeResponse { Status = status, Message = message };
                var json = JsonSerializer.Serialize(errorResp);
                await writer.WriteLineAsync(json).ConfigureAwait(false);
            }
            catch { }
        }

        /// <summary>
        /// Disposes resources used by the pipe server.
        /// </summary>
        public void Dispose()
        {
            Stop();
            try { listenTask?.Wait(500); } catch { }
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
