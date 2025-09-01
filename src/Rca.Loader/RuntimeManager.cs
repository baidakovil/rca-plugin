using Autodesk.Revit.UI;
using Rca.Loader.Contracts;
using Rca.Loader.Contracts.Protocol;
using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Rca.Loader
{
    /// <summary>
    /// Manages the lifecycle of hot-reloadable plugin runtime.
    /// </summary>
    internal class RuntimeManager
    {
        private HotReloadAssemblyLoadContext currentContext;
        private IPluginRuntime currentRuntime;
        private WeakReference contextWeakRef;
        private UIControlledApplication uiApplication;
        private NamedPipeServerStream pipeServer;
        private CancellationTokenSource pipeServerCts;
        private readonly object lockObject = new object();

        /// <summary>
        /// Starts the named pipe server for build notifications.
        /// </summary>
        public void StartPipeServer()
        {
            try
            {
                pipeServerCts = new CancellationTokenSource();
                Task.Run(() => RunPipeServerAsync(pipeServerCts.Token));
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Loader", $"Failed to start pipe server: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads the initial runtime.
        /// </summary>
        public void LoadRuntime(UIControlledApplication application)
        {
            uiApplication = application;
            LoadRuntimeInternal();
        }

        /// <summary>
        /// Manually triggers a runtime reload.
        /// </summary>
        public void Reload(bool force = false)
        {
            LoadRuntimeInternal(force);
        }

        /// <summary>
        /// Shuts down the runtime manager.
        /// </summary>
        public void Shutdown()
        {
            lock (lockObject)
            {
                // Stop pipe server
                pipeServerCts?.Cancel();
                pipeServer?.Dispose();

                // Shutdown current runtime
                if (currentRuntime != null)
                {
                    try
                    {
                        currentRuntime.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("RCA Loader", $"Error shutting down runtime: {ex.Message}");
                    }
                }

                // Unload context
                UnloadCurrentContext();
            }
        }

        /// <summary>
        /// Loads or reloads the runtime implementation.
        /// </summary>
        private void LoadRuntimeInternal(bool force = false)
        {
            lock (lockObject)
            {
                try
                {
                    // Find runtime manifest
                    var manifest = ReadRuntimeManifest();
                    if (manifest == null)
                    {
                        TaskDialog.Show("RCA Loader", "No runtime manifest found. Build Rca.Runtime project first.");
                        return;
                    }

                    // Construct assembly path
                    var assemblyPath = Path.Combine(manifest.Folder, manifest.Assembly);
                    if (!File.Exists(assemblyPath))
                    {
                        TaskDialog.Show("RCA Loader", $"Runtime assembly not found: {assemblyPath}");
                        return;
                    }

                    // Shutdown previous runtime
                    if (currentRuntime != null)
                    {
                        try
                        {
                            currentRuntime.Shutdown();
                        }
                        catch (Exception ex)
                        {
                            TaskDialog.Show("RCA Loader", $"Error shutting down previous runtime: {ex.Message}");
                        }
                    }

                    // Unload previous context
                    UnloadCurrentContext();

                    // Create new context and load assembly
                    var contextName = $"RCA Runtime {DateTime.Now:yyyyMMdd_HHmmss}";
                    currentContext = new HotReloadAssemblyLoadContext(contextName);
                    contextWeakRef = currentContext.WeakReference;

                    var assembly = currentContext.LoadFromAssemblyPath(assemblyPath);

                    // Find and instantiate runtime
                    var runtimeType = assembly.GetTypes()
                        .FirstOrDefault(t => typeof(IPluginRuntime).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                    if (runtimeType == null)
                    {
                        TaskDialog.Show("RCA Loader", "No IPluginRuntime implementation found in dynamic assembly.");
                        return;
                    }

                    currentRuntime = (IPluginRuntime)Activator.CreateInstance(runtimeType);

                    // Initialize runtime
                    currentRuntime.OnLoaded();
                    currentRuntime.Initialize(uiApplication);

                    // Log success
                    var message = $"Runtime loaded successfully: {currentRuntime.Version} from {contextName}";
                    System.Diagnostics.Debug.WriteLine($"[RCA Loader] {message}");

#if DEBUG
                    // Show success notification in debug builds
                    TaskDialog.Show("RCA Loader", message);
#endif
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("RCA Loader Error", $"Failed to load runtime: {ex.Message}\n\n{ex.StackTrace}");
                }
            }
        }

        /// <summary>
        /// Unloads the current assembly load context.
        /// </summary>
        private void UnloadCurrentContext()
        {
            if (currentContext != null)
            {
                currentContext.Unload();
                currentContext = null;
                currentRuntime = null;

                // Force garbage collection to unload context
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

#if DEBUG
                // Verify collection in debug mode
                if (contextWeakRef != null)
                {
                    Task.Delay(100).ContinueWith(_ =>
                    {
                        var collected = !contextWeakRef.IsAlive;
                        System.Diagnostics.Debug.WriteLine($"[RCA Loader] ALC_COLLECTED: {collected}");
                    });
                }
#endif
            }
        }

        /// <summary>
        /// Reads the runtime manifest file.
        /// </summary>
        private RuntimeManifest ReadRuntimeManifest()
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var manifestPath = Path.Combine(appDataPath, "RCA", HotReloadConstants.StagingDirectoryName, HotReloadConstants.ManifestFileName);

                if (!File.Exists(manifestPath))
                    return null;

                var json = File.ReadAllText(manifestPath);
                return JsonSerializer.Deserialize<RuntimeManifest>(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RCA Loader] Error reading manifest: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Runs the named pipe server for build notifications.
        /// </summary>
        private async Task RunPipeServerAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using (pipeServer = new NamedPipeServerStream(HotReloadConstants.PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                    {
                        await pipeServer.WaitForConnectionAsync(cancellationToken);
                        
                        using (var reader = new StreamReader(pipeServer))
                        using (var writer = new StreamWriter(pipeServer) { AutoFlush = true })
                        {
                            // Send connection acknowledgment
                            var ackEvent = new EventMessage
                            {
                                Event = "RELOAD_ACCEPTED",
                                Timestamp = DateTime.Now.ToString("O"),
                                Data = new { Message = "Connected to RCA Loader" }
                            };
                            await writer.WriteLineAsync(JsonSerializer.Serialize(ackEvent));

                            // Read command
                            var message = await reader.ReadLineAsync();
                            if (!string.IsNullOrEmpty(message))
                            {
                                try
                                {
                                    var command = JsonSerializer.Deserialize<CommandMessage>(message);
                                    if (command?.Command == "RELOAD")
                                    {
                                        // Send reload start event
                                        var startEvent = new EventMessage
                                        {
                                            Event = "RELOAD_START",
                                            Timestamp = DateTime.Now.ToString("O"),
                                            Data = command.Payload
                                        };
                                        await writer.WriteLineAsync(JsonSerializer.Serialize(startEvent));

                                        // Perform reload directly
                                        try
                                        {
                                            LoadRuntimeInternal(true);
                                            
                                            // Send success event
                                            var doneEvent = new EventMessage
                                            {
                                                Event = "RELOAD_DONE",
                                                Timestamp = DateTime.Now.ToString("O"),
                                                Data = new { Success = true }
                                            };
                                            await writer.WriteLineAsync(JsonSerializer.Serialize(doneEvent));
                                        }
                                        catch (Exception ex)
                                        {
                                            // Send failure event
                                            var failEvent = new EventMessage
                                            {
                                                Event = "RELOAD_FAIL",
                                                Timestamp = DateTime.Now.ToString("O"),
                                                Data = new ErrorMessage
                                                {
                                                    Error = "ReloadFailed",
                                                    Message = ex.Message,
                                                    StackTrace = ex.StackTrace
                                                }
                                            };
                                            await writer.WriteLineAsync(JsonSerializer.Serialize(failEvent));
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    var errorEvent = new EventMessage
                                    {
                                        Event = "RUNTIME_ERROR",
                                        Timestamp = DateTime.Now.ToString("O"),
                                        Data = new ErrorMessage
                                        {
                                            Error = "CommandProcessingError",
                                            Message = ex.Message,
                                            StackTrace = ex.StackTrace
                                        }
                                    };
                                    await writer.WriteLineAsync(JsonSerializer.Serialize(errorEvent));
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    System.Diagnostics.Debug.WriteLine($"[RCA Loader] Pipe server error: {ex.Message}");
                    await Task.Delay(1000, cancellationToken); // Wait before retrying
                }
            }
        }
    }
}