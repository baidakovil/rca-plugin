using System;
using System.IO.Pipes;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Rca.Loader.Contracts;

namespace Rca.Loader
{
    public class LoaderApp : IExternalApplication
    {
        private const string PipeName = "RCA_PIPE";
        private const string RuntimeFileName = "Rca.Runtime.dll";
        private static readonly string RuntimeDeployRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RCA", "Runtime");

        private CancellationTokenSource? pipeCts;
        private RuntimeLoadContext? currentContext;
        private IRuntime? currentRuntime;

        internal static LoaderApp? Instance { get; private set; }

        public LoaderApp()
        {
            Instance = this;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                StartPipeServer();
                BuildRibbon(application);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RCA Loader Error", ex.ToString());
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                pipeCts?.Cancel();
                UnloadRuntime();
            }
            catch { }
            return Result.Succeeded;
        }

        private void BuildRibbon(UIControlledApplication app)
        {
            const string tabName = "RCA";
            const string panelName = "Loader";
            try { app.CreateRibbonTab(tabName); } catch { }
            var panel = app.CreateRibbonPanel(tabName, panelName);

            // Button: Open Standalone Window
            var openBtn = new PushButtonData(
                "RCA_OpenStandalone",
                "Open\nAssistant",
                Assembly.GetExecutingAssembly().Location,
                typeof(OpenStandaloneWindowCommand).FullName);
            var openPush = panel.AddItem(openBtn) as PushButton;
            AssignEmbeddedIcons(openPush,
                smallFileName: "OpenAssistant16.png",
                largeFileName: "OpenAssistant32.png",
                tooltip: "Open the RCA standalone assistant window.");

            // Button: Reload Runtime (latest)
            var reloadBtn = new PushButtonData(
                "RCA_ReloadRuntime",
                "Reload\nRuntime",
                Assembly.GetExecutingAssembly().Location,
                typeof(ReloadRuntimeCommand).FullName);
            var reloadPush = panel.AddItem(reloadBtn) as PushButton;
            AssignEmbeddedIcons(reloadPush,
                smallFileName: "ReloadRuntime16.png",
                largeFileName: "ReloadRuntime32.png",
                tooltip: "Reload the latest deployed runtime.");
        }

        private static void AssignEmbeddedIcons(PushButton? button, string smallFileName, string largeFileName, string? tooltip = null)
        {
            if (button == null) return;

            try
            {
                var asm = Assembly.GetExecutingAssembly();
                button.Image = LoadEmbeddedBitmap(asm, smallFileName) ?? button.Image;
                button.LargeImage = LoadEmbeddedBitmap(asm, largeFileName) ?? button.LargeImage;

                if (!string.IsNullOrWhiteSpace(tooltip))
                {
                    button.ToolTip = tooltip;
                }
            }
            catch
            {
                // Ignore icon load issues to avoid blocking add-in load.
            }
        }

        private static BitmapImage? LoadEmbeddedBitmap(Assembly asm, string fileName)
        {
            // Common resource name patterns
            // Example final: Rca.Loader.Resources.OpenAssistant16.png
            var asmName = asm.GetName().Name;
            var candidates = new[]
            {
                $"{asmName}.Resources.{fileName}",
                $"Rca.Loader.Resources.{fileName}" // fallback explicit root namespace
            };

            foreach (var resName in candidates.Distinct())
            {
                using var stream = asm.GetManifestResourceStream(resName);
                if (stream == null) continue;

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = stream;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }

            return null;
        }

        private void StartPipeServer()
        {
            pipeCts = new CancellationTokenSource();
            _ = Task.Run(() => ListenLoopAsync(pipeCts.Token));
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                try
                {
                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    using var reader = new StreamReader(server);
                    using var writer = new StreamWriter(server) { AutoFlush = true };
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line == null) continue;
                    var cmd = JsonSerializer.Deserialize<PipeCommand>(line);
                    if (cmd == null) continue;
                    switch (cmd.Command)
                    {
                        case "RELOAD":
                            var path = cmd.Payload;
                            var result = ReloadRuntime(path, out var errorMessage);
                            var resp = new PipeResponse { Status = result ? "OK" : "ERROR", Message = errorMessage ?? "" };
                            await writer.WriteLineAsync(JsonSerializer.Serialize(resp)).ConfigureAwait(false);
                            break;
                        case "STATUS":
                            var status = new PipeResponse { Status = currentRuntime != null ? "LOADED" : "EMPTY", Message = currentContext?.RuntimePath ?? string.Empty };
                            await writer.WriteLineAsync(JsonSerializer.Serialize(status)).ConfigureAwait(false);
                            break;
                        default:
                            await writer.WriteLineAsync(JsonSerializer.Serialize(new PipeResponse { Status = "ERROR", Message = "Unknown command" })).ConfigureAwait(false);
                            break;
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    TaskDialog.Show("RCA Loader Pipe Error", ex.Message);
                }
            }
        }

        private bool ReloadRuntime(string? folderPath, out string? error)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath)) { error = "Folder path missing"; return false; }
                var runtimeDll = Path.Combine(folderPath, RuntimeFileName);
                if (!File.Exists(runtimeDll)) { error = $"Runtime dll not found: {runtimeDll}"; return false; }

                UnloadRuntime();

                // Create a new context for loading the runtime
                currentContext = new RuntimeLoadContext();
                
                // Load the runtime DLL into our context
                var asm = currentContext.LoadFromAssemblyPath(runtimeDll);
                
                // Look for RuntimeEntry type directly by name
                Type? rtType = null;
                foreach (var type in asm.GetTypes())
                {
                    if (type.Name == "RuntimeEntry" && !type.IsAbstract)
                    {
                        rtType = type;
                        break;
                    }
                }
                
                if (rtType == null) 
                { 
                    // Log all available types for debugging
                    var allTypes = string.Join(", ", asm.GetTypes().Select(t => t.FullName));
                    error = $"RuntimeEntry class not found. Available types: {allTypes}"; 
                    return false; 
                }
                
                // Create an instance of RuntimeEntry
                var instance = Activator.CreateInstance(rtType);
                
                // Use reflection to invoke methods on the RuntimeEntry instance
                var initMethod = rtType.GetMethod("Initialize");
                if (initMethod == null)
                {
                    error = "Initialize method not found on RuntimeEntry";
                    return false;
                }
                
                // Store the runtime path and instance for later use
                currentContext.SetRuntimePath(runtimeDll);
                currentContext.SetRuntimeInstance(instance);
                
                // Call Initialize
                initMethod.Invoke(instance, null);
                
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
        }

        private void UnloadRuntime()
        {
            try 
            {
                if (currentContext?.RuntimeInstance != null)
                {
                    var rtType = currentContext.RuntimeInstance.GetType();
                    var shutdownMethod = rtType.GetMethod("Shutdown");
                    shutdownMethod?.Invoke(currentContext.RuntimeInstance, null);
                }
            } 
            catch { }
            
            currentRuntime = null;
            
            if (currentContext != null)
            {
                currentContext.Unload();
                currentContext = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        private bool ShowStandaloneWindow(out string? error)
        {
            if (currentContext == null)
            {
                error = "Runtime not loaded";
                return false;
            }
            try
            {
                var asm = currentContext.Assemblies.FirstOrDefault(a => !a.IsDynamic && string.Equals(Path.GetFileName(a.Location), RuntimeFileName, StringComparison.OrdinalIgnoreCase));
                if (asm == null)
                {
                    error = "Merged runtime assembly not found in context";
                    return false;
                }
                
                // Find the standalone window type by name
                Type? winType = null;
                foreach (var type in asm.GetTypes())
                {
                    if (type.Name == "RcaStandaloneWindow" && typeof(Window).IsAssignableFrom(type))
                    {
                        winType = type;
                        break;
                    }
                }
                
                if (winType == null)
                {
                    // Log all available window types
                    var availableWindowTypes = string.Join(", ", 
                        asm.GetTypes()
                        .Where(t => typeof(Window).IsAssignableFrom(t))
                        .Select(t => t.FullName));
                    
                    error = $"RcaStandaloneWindow type not found. Available window types: {availableWindowTypes}";
                    return false;
                }
                
                if (Activator.CreateInstance(winType) is Window window)
                {
                    window.Show();
                    error = null;
                    return true;
                }
                error = "Failed to create window instance";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
        }

        private bool ReloadLatest(out string? error)
        {
            if (!Directory.Exists(RuntimeDeployRoot))
            {
                error = $"Runtime root not found: {RuntimeDeployRoot}";
                return false;
            }
            var latest = Directory.GetDirectories(RuntimeDeployRoot)
                .OrderByDescending(d => d)
                .FirstOrDefault();
            if (latest == null)
            {
                error = "No runtime versions found";
                return false;
            }
            return ReloadRuntime(latest, out error);
        }

        [Transaction(TransactionMode.Manual)]
        internal class OpenStandaloneWindowCommand : IExternalCommand
        {
            public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
            {
                if (Instance == null)
                {
                    message = "Loader instance unavailable";
                    return Result.Failed;
                }
                if (!Instance.ShowStandaloneWindow(out var error))
                {
                    TaskDialog.Show("RCA Loader", error ?? "Unknown error opening window");
                    message = error ?? string.Empty;
                    return Result.Failed;
                }
                return Result.Succeeded;
            }
        }

        [Transaction(TransactionMode.Manual)]
        internal class ReloadRuntimeCommand : IExternalCommand
        {
            public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
            {
                if (Instance == null)
                {
                    message = "Loader instance unavailable";
                    return Result.Failed;
                }
                if (!Instance.ReloadLatest(out var error))
                {
                    TaskDialog.Show("RCA Loader", error ?? "Reload failed");
                    message = error ?? string.Empty;
                    return Result.Failed;
                }
                TaskDialog.Show("RCA Loader", "Runtime reloaded successfully");
                return Result.Succeeded;
            }
        }

        private record PipeCommand(string Command, string? Payload);
        private record PipeResponse { public string Status { get; set; } = string.Empty; public string Message { get; set; } = string.Empty; }

        private class RuntimeLoadContext : AssemblyLoadContext
        {
            private string? runtimePath;
            private object? runtimeInstance;
            
            public string RuntimePath => runtimePath ?? "";
            public object? RuntimeInstance => runtimeInstance;
            
            public void SetRuntimePath(string path)
            {
                runtimePath = path;
            }
            
            public void SetRuntimeInstance(object instance)
            {
                runtimeInstance = instance;
            }

            public RuntimeLoadContext() : base(isCollectible: true)
            {
                // Register resolving event
                Resolving += OnResolving;
            }
            
            private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName)
            {
                if (string.IsNullOrEmpty(runtimePath))
                    return null;
                
                var baseDir = Path.GetDirectoryName(runtimePath)!;
                
                // Special handling for the Contracts assembly to avoid type identity issues
                if (assemblyName.Name == "Rca.Loader.Contracts")
                {
                    // Look for the contracts assembly in the default context
                    var contractsAsm = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => !a.IsDynamic && a.GetName().Name == assemblyName.Name);
                    
                    if (contractsAsm != null)
                        return contractsAsm;
                }
                
                // Try to load from the runtime directory
                var candidate = Path.Combine(baseDir, assemblyName.Name + ".dll");
                if (File.Exists(candidate))
                {
                    return LoadFromAssemblyPath(candidate);
                }
                
                return null;
            }

            protected override Assembly? Load(AssemblyName assemblyName)
            {
                // Prefer to use the Resolving event handler
                return null;
            }
        }
    }
}
