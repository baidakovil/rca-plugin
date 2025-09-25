using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using Autodesk.Revit.UI;
using Rca.Loader.AssemblyManagement;
using Rca.Loader.Infrastructure;

namespace Rca.Loader.Restart
{
    /// <summary>
    /// Manages the graceful restart of Revit when loader assemblies are updated.
    /// </summary>
    public class RestartManager
    {
        private readonly AssemblyStatusManager _statusManager;
        private const string PowerShellPath = "powershell.exe";
        private const string ScriptFilename = "RestartRevitGraceful.ps1";
        private static readonly string ScriptPath = Path.Combine(
            Path.GetDirectoryName(typeof(RestartManager).Assembly.Location) ?? string.Empty,
            "..", "..", "..", "build", "Scripts", ScriptFilename);

        private const string LoaderSourceHashFile = "source-hash.loader.txt";

        /// <summary>
        /// Initializes a new instance of the <see cref="RestartManager"/> class.
        /// </summary>
        /// <param name="statusManager">The assembly status manager.</param>
        public RestartManager(AssemblyStatusManager statusManager)
        {
            _statusManager = statusManager ?? throw new ArgumentNullException(nameof(statusManager));
        }

        /// <summary>
        /// Shows a dialog with restart options and countdown.
        /// </summary>
        public bool ShowRestartDialog(int countdownSeconds = 10)
        {
            try
            {
                var taskDialog = new TaskDialog("Revit Restart Required")
                {
                    MainIcon = TaskDialogIcon.TaskDialogIconWarning,
                    MainInstruction = "Loader assembly has been updated",
                    MainContent = $"Revit needs to restart to load the updated assembly. The restart will begin in {countdownSeconds} seconds.\n\n" +
                                  "Your work will be saved automatically before closing.\n\n" +
                                  "Do you want to proceed with the restart?",
                    CommonButtons = TaskDialogCommonButtons.Cancel,
                    DefaultButton = TaskDialogResult.Cancel
                };

                taskDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Restart now", "Restart Revit immediately");
                taskDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Restart later", "Continue working and restart manually later");

                var countdown = countdownSeconds;
                var timer = new Timer(_ => 
                {
                    try
                    {
                        var remaining = Interlocked.Decrement(ref countdown);
                        if (remaining >= 0)
                        {
                            taskDialog.MainContent = $"Revit needs to restart to load the updated assembly. The restart will begin in {remaining} seconds.\n\n" +
                                                "Your work will be saved automatically before closing.\n\n" +
                                                "Do you want to proceed with the restart?";
                        }
                    }
                    catch
                    {
                        // Ignore timer callback errors
                    }
                }, null, 0, 1000);

                var result = taskDialog.Show();

                timer.Dispose();

                switch (result)
                {
                    case TaskDialogResult.CommandLink1:
                        return ExecuteRestartScript(out _);
                    case TaskDialogResult.CommandLink2:
                        TaskDialog.Show("Restart Later", "Please remember to restart Revit manually to load the updated assembly.");
                        return false;
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"An error occurred while showing the restart dialog: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Executes the PowerShell restart script.
        /// </summary>
        public bool ExecuteRestartScript(out string error)
        {
            try
            {
                string sourcePath = _statusManager.GetLatestTempDllFolder();
                if (string.IsNullOrEmpty(sourcePath))
                {
                    error = "Source path not found";
                    return false;
                }

                string targetPath = LoaderConstants.RevitAddinDir;
                if (string.IsNullOrEmpty(targetPath) || !Directory.Exists(targetPath))
                {
                    error = "Target path not found";
                    return false;
                }

                if (!File.Exists(ScriptPath))
                {
                    var altPath = Path.Combine(Directory.GetCurrentDirectory(), "build", "Scripts", ScriptFilename);
                    if (!File.Exists(altPath))
                    {
                        error = $"Restart script not found at: {ScriptPath}";
                        return false;
                    }
                    ExecuteScript(altPath, sourcePath, targetPath, out error);
                    return string.IsNullOrEmpty(error);
                }

                ExecuteScript(ScriptPath, sourcePath, targetPath, out error);
                return string.IsNullOrEmpty(error);
            }
            catch (Exception ex)
            {
                error = $"Error executing restart script: {ex.Message}";
                return false;
            }
        }

        private void ExecuteScript(string scriptPath, string sourcePath, string targetPath, out string error)
        {
            error = string.Empty;
            try
            {
                var revitProcess = Process.GetCurrentProcess();
                string revitExecutable = revitProcess.MainModule?.FileName ?? string.Empty;
                if (string.IsNullOrEmpty(revitExecutable))
                {
                    error = "Could not determine Revit executable path";
                    return;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = PowerShellPath,
                    Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" " +
                               $"-SourcePath \"{sourcePath}\" " +
                               $"-TargetPath \"{targetPath}\" " +
                               $"-RevitExecutable \"{revitExecutable}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    error = "Failed to start PowerShell process";
                    return;
                }
            }
            catch (Exception ex)
            {
                error = $"Error executing script: {ex.Message}";
            }
        }

        /// <summary>
        /// Validates that the loader assembly was copied successfully by comparing source-hash files.
        /// </summary>
        public bool ValidateAssemblyCopy(string sourcePath, string targetPath)
        {
            try
            {
                var loaderSourcePath = Path.Combine(sourcePath, LoaderConstants.LoaderFileName);
                var loaderTargetPath = Path.Combine(targetPath, LoaderConstants.LoaderFileName);

                if (!File.Exists(loaderTargetPath)) return false;

                // Compare loader-specific source-hash files if present
                var sourceHash = ReadSourceHashFromDir(sourcePath, LoaderSourceHashFile);
                var targetHash = ReadSourceHashFromDir(targetPath, LoaderSourceHashFile);

                if (!string.IsNullOrEmpty(sourceHash) && !string.IsNullOrEmpty(targetHash))
                {
                    return string.Equals(sourceHash, targetHash, StringComparison.OrdinalIgnoreCase);
                }

                // Fallback to binary compare if no source-hash files
                try
                {
                    var srcBytes = File.ReadAllBytes(loaderSourcePath);
                    var tgtBytes = File.ReadAllBytes(loaderTargetPath);
                    return srcBytes.Length == tgtBytes.Length && srcBytes.SequenceEqual(tgtBytes);
                }
                catch
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error validating assembly copy: {ex.Message}");
                return false;
            }
        }

        private string ReadSourceHashFromDir(string dir, string fileName = LoaderSourceHashFile)
        {
            try
            {
                if (string.IsNullOrEmpty(dir)) return string.Empty;
                var candidate = Path.Combine(dir, fileName);
                if (File.Exists(candidate)) return File.ReadAllText(candidate).Trim();
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
