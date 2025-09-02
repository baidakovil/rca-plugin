using System;
using System.IO.Pipes;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq; // for FirstOrDefault, ordering
using System.Windows; // for Window
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB; // for ElementSet
using Rca.Loader.Contracts;

namespace Rca.Loader;

public class LoaderApp : IExternalApplication
{
    private const string PipeName = "RCA_PIPE"; // TODO: move to config
    private const string RuntimeFileName = "Rca.Runtime.dll"; // produced by ILRepack
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
        panel.AddItem(openBtn);

        // Button: Reload Runtime (latest)
        var reloadBtn = new PushButtonData(
            "RCA_ReloadRuntime",
            "Reload\nRuntime",
            Assembly.GetExecutingAssembly().Location,
            typeof(ReloadRuntimeCommand).FullName);
        panel.AddItem(reloadBtn);
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
                        var path = cmd.Payload; // folder path containing runtime dll
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

            currentContext = new RuntimeLoadContext(runtimeDll);
            var asm = currentContext.LoadFromAssemblyPath(runtimeDll);
            var rtType = asm.GetTypes().FirstOrDefault(t => typeof(IRuntime).IsAssignableFrom(t) && !t.IsAbstract);
            if (rtType == null) { error = "IRuntime implementation not found"; return false; }
            currentRuntime = (IRuntime)Activator.CreateInstance(rtType)!;
            currentRuntime.Initialize();
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
        try { currentRuntime?.Shutdown(); } catch { }
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
            // Find the merged runtime assembly (the one that contains our UI window)
            var asm = currentContext.Assemblies.FirstOrDefault(a => !a.IsDynamic && string.Equals(Path.GetFileName(a.Location), RuntimeFileName, StringComparison.OrdinalIgnoreCase));
            if (asm == null)
            {
                error = "Merged runtime assembly not found in context";
                return false;
            }
            var winType = asm.GetTypes().FirstOrDefault(t => t.Name == "RcaStandaloneWindow");
            if (winType == null)
            {
                error = "RcaStandaloneWindow type not found";
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
            error = ex.Message;
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

    // Command implementations -------------------------------------------------

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

    // Pipe command/response records ------------------------------------------
    private record PipeCommand(string Command, string? Payload);
    private record PipeResponse { public string Status { get; set; } = string.Empty; public string Message { get; set; } = string.Empty; }

    private class RuntimeLoadContext : AssemblyLoadContext
    {
        public string RuntimePath { get; }
        private readonly string baseDir;

        public RuntimeLoadContext(string runtimePath) : base(isCollectible: true)
        {
            RuntimePath = runtimePath;
            baseDir = Path.GetDirectoryName(runtimePath)!;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Ensure the shared contracts assembly is always taken from the default context
            if (assemblyName.Name == "Rca.Loader.Contracts")
            {
                // Return the already loaded contracts assembly (type identity shared with loader)
                var loaded = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a => !a.IsDynamic && a.GetName().Name == assemblyName.Name);
                if (loaded != null) return loaded;
                // As fallback try normal load into default context
                return Assembly.Load(assemblyName);
            }

            var candidate = Path.Combine(baseDir, assemblyName.Name + ".dll");
            if (File.Exists(candidate))
            {
                return LoadFromAssemblyPath(candidate);
            }
            return null; // let default resolution (framework) handle
        }
    }
}
