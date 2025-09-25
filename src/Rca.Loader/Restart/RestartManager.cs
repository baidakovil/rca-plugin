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

        // LoaderSourceHashFile removed: validation uses AssemblyMetadata 'SourceHash' only.

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
        /// Validates that the loader assembly was copied successfully by comparing embedded source hashes
        /// (AssemblyMetadata SourceHash) or by reading LoaderVersion info files next to the DLL. Falls back to
        /// binary compare if neither metadata nor info files exist.
        /// </summary>
        public bool ValidateAssemblyCopy(string sourcePath, string targetPath)
        {
            try
            {
                var loaderFileName = LoaderConstants.LoaderFileName;
                var loaderSourcePath = Path.Combine(sourcePath, loaderFileName);
                var loaderTargetPath = Path.Combine(targetPath, loaderFileName);

                if (!File.Exists(loaderTargetPath)) return false;

                // Only validate by embedded assembly metadata 'SourceHash'.
                // If metadata is missing or different, do not fall back to other methods —
                // developer should investigate the mismatch.
                bool err1 = false, err2 = false;
                string? srcMetaHash = null;
                string? tgtMetaHash = null;

                var ok1 = AssemblyMetadataReader.TryGetAssemblyMetadata(loaderSourcePath, "SourceHash", out srcMetaHash, out err1);
                var ok2 = AssemblyMetadataReader.TryGetAssemblyMetadata(loaderTargetPath, "SourceHash", out tgtMetaHash, out err2);

                if (err1 || err2)
                {
                    var msg = $"Error reading AssemblyMetadata 'SourceHash' (sourceError={err1}, targetError={err2})";
                    Debug.WriteLine($"ValidateAssemblyCopy: {msg}");
                    throw new InvalidOperationException(msg);
                }

                // If no metadata present on either side, validation fails but no error thrown.
                if (string.IsNullOrEmpty(srcMetaHash) || string.IsNullOrEmpty(tgtMetaHash))
                {
                    Debug.WriteLine("ValidateAssemblyCopy: missing SourceHash metadata on source or target assembly");
                    return false;
                }

                var srcShort = GetShortHash(srcMetaHash!);
                var tgtShort = GetShortHash(tgtMetaHash!);
                if (string.Equals(srcShort, tgtShort, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                Debug.WriteLine($"ValidateAssemblyCopy: metadata hash mismatch (src={srcShort}, tgt={tgtShort})");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error validating assembly copy: {ex.Message}");
                return false;
            }
        }

        private static string GetShortHash(string hash)
        {
            if (string.IsNullOrEmpty(hash)) return string.Empty;
            var cleaned = hash.Trim();
            if (cleaned.Length > 6) return cleaned.Substring(0, 6);
            return cleaned;
        }

        // Note: previous implementations read LoaderVersion - {hash}.txt files and did binary fallbacks.
        // Current policy: rely only on embedded AssemblyMetadata(SourceHash). If absent or mismatched,
        // validation fails and developer must investigate.
    }
}
