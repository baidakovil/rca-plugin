using System;
using System.IO.Pipes;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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

        public PipeServer(string pipeName, CommandHandler handler)
        {
            this.pipeName = pipeName;
            this.handler = handler;
        }

        public void Start()
        {
            pipeCts = new CancellationTokenSource();
            listenTask = Task.Run(() => ListenLoopAsync(pipeCts.Token));
        }

        public void Stop()
        {
            pipeCts?.Cancel();
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                try
                {
                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    using var reader = new StreamReader(server);
                    using var writer = new StreamWriter(server) { AutoFlush = true };
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line == null) continue;
                    var cmd = JsonSerializer.Deserialize<PipeCommand>(line);
                    if (cmd == null) continue;
                    var resp = await handler(cmd);
                    await writer.WriteLineAsync(JsonSerializer.Serialize(resp)).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    // Optionally log error
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }

    public record PipeCommand(string Command, string? Payload);
    public record PipeResponse { public string Status { get; set; } = string.Empty; public string Message { get; set; } = string.Empty; }
}
