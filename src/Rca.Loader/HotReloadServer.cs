using Autodesk.Revit.UI;
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
    /// NamedPipe server for hot-reload commands and communication with external tools.
    /// </summary>
    public class HotReloadServer
    {
        private const string PipeName = "rca.hotreload";
        private readonly RuntimeHost runtimeHost;
        private CancellationTokenSource cancellationTokenSource;
        private Task serverTask;
        private ExternalEvent externalEvent;
        private HotReloadEventHandler eventHandler;

        /// <summary>
        /// Initializes a new instance of the HotReloadServer.
        /// </summary>
        public HotReloadServer(RuntimeHost runtimeHost)
        {
            this.runtimeHost = runtimeHost ?? throw new ArgumentNullException(nameof(runtimeHost));
            eventHandler = new HotReloadEventHandler(this);
            externalEvent = ExternalEvent.Create(eventHandler);
        }

        /// <summary>
        /// Starts the NamedPipe server.
        /// </summary>
        public void Start()
        {
            try
            {
                Console.WriteLine("[HotReload] Starting NamedPipe server...");
                cancellationTokenSource = new CancellationTokenSource();
                serverTask = Task.Run(() => RunServerAsync(cancellationTokenSource.Token));
                Console.WriteLine($"[HotReload] NamedPipe server started on pipe: {PipeName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HotReload] Failed to start server: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops the NamedPipe server.
        /// </summary>
        public void Stop()
        {
            try
            {
                Console.WriteLine("[HotReload] Stopping NamedPipe server...");
                cancellationTokenSource?.Cancel();
                serverTask?.Wait(5000); // Wait up to 5 seconds
                externalEvent?.Dispose();
                Console.WriteLine("[HotReload] NamedPipe server stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HotReload] Error stopping server: {ex.Message}");
            }
        }

        /// <summary>
        /// Runs the NamedPipe server loop.
        /// </summary>
        private async Task RunServerAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using (var server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                    {
                        Console.WriteLine("[HotReload] Waiting for client connection...");
                        await server.WaitForConnectionAsync(cancellationToken);
                        Console.WriteLine("[HotReload] Client connected");

                        await HandleClientAsync(server, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break; // Normal shutdown
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[HotReload] Server error: {ex.Message}");
                    await Task.Delay(1000, cancellationToken); // Brief pause before retrying
                }
            }
        }

        /// <summary>
        /// Handles communication with a connected client.
        /// </summary>
        private async Task HandleClientAsync(NamedPipeServerStream server, CancellationToken cancellationToken)
        {
            try
            {
                using (var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true))
                using (var writer = new StreamWriter(server, Encoding.UTF8, leaveOpen: true))
                {
                    writer.AutoFlush = true;

                    // Send initial status
                    await SendResponseAsync(writer, new { Type = "STATUS", State = "Connected" });

                    string line;
                    while (!cancellationToken.IsCancellationRequested && (line = await reader.ReadLineAsync()) != null)
                    {
                        Console.WriteLine($"[HotReload] Received: {line}");
                        await ProcessCommandAsync(writer, line);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HotReload] Client handling error: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a received command.
        /// </summary>
        private async Task ProcessCommandAsync(StreamWriter writer, string commandJson)
        {
            try
            {
                var command = JsonSerializer.Deserialize<HotReloadCommand>(commandJson);
                
                switch (command?.Command?.ToUpperInvariant())
                {
                    case "PING":
                        await SendResponseAsync(writer, new { Type = "STATUS", State = "Ready", Version = runtimeHost.CurrentRuntime?.Version ?? "Unknown" });
                        break;

                    case "RELOAD":
                        eventHandler.SetPendingOperation(() => {
                            var success = runtimeHost.ReloadRuntime();
                            return new { Type = "STATUS", State = success ? "Ready" : "Error", Version = runtimeHost.CurrentRuntime?.Version ?? "Unknown" };
                        }, writer);
                        externalEvent.Raise();
                        break;

                    case "RUN_TEST":
                        await SendResponseAsync(writer, new { Type = "TEST_RESULT", Test = command.Filter ?? "Unknown", Outcome = "NotImplemented" });
                        break;

                    case "STATUS":
                        var status = runtimeHost.CurrentRuntime?.GetStatus() ?? "No runtime loaded";
                        await SendResponseAsync(writer, new { Type = "STATUS", State = "Ready", Status = status, Version = runtimeHost.CurrentRuntime?.Version ?? "Unknown" });
                        break;

                    default:
                        await SendResponseAsync(writer, new { Type = "ERROR", Message = $"Unknown command: {command?.Command}" });
                        break;
                }
            }
            catch (Exception ex)
            {
                await SendResponseAsync(writer, new { Type = "ERROR", Message = ex.Message });
            }
        }

        /// <summary>
        /// Sends a response to the client.
        /// </summary>
        private async Task SendResponseAsync(StreamWriter writer, object response)
        {
            try
            {
                var json = JsonSerializer.Serialize(response);
                await writer.WriteLineAsync(json);
                Console.WriteLine($"[HotReload] Sent: {json}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HotReload] Failed to send response: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Command structure for hot-reload operations.
    /// </summary>
    public class HotReloadCommand
    {
        public string Command { get; set; }
        public string Filter { get; set; }
    }

    /// <summary>
    /// ExternalEvent handler to marshal operations onto Revit UI thread.
    /// </summary>
    internal class HotReloadEventHandler : IExternalEventHandler
    {
        private readonly HotReloadServer server;
        private Func<object> pendingOperation;
        private StreamWriter pendingWriter;

        public HotReloadEventHandler(HotReloadServer server)
        {
            this.server = server;
        }

        public void SetPendingOperation(Func<object> operation, StreamWriter writer)
        {
            pendingOperation = operation;
            pendingWriter = writer;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                if (pendingOperation != null && pendingWriter != null)
                {
                    var result = pendingOperation();
                    var json = JsonSerializer.Serialize(result);
                    pendingWriter.WriteLine(json);
                    Console.WriteLine($"[HotReload] ExternalEvent result: {json}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HotReload] ExternalEvent execution error: {ex.Message}");
                if (pendingWriter != null)
                {
                    var errorResponse = new { Type = "ERROR", Message = ex.Message };
                    var json = JsonSerializer.Serialize(errorResponse);
                    pendingWriter.WriteLine(json);
                }
            }
            finally
            {
                pendingOperation = null;
                pendingWriter = null;
            }
        }

        public string GetName() => "RCA Hot Reload Event Handler";
    }
}