using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Autodesk.Revit.UI;
using Rca.Loader.Infrastructure;
using Rca.Loader.Logging;
using Rca.Loader.AssemblyManagement;
using Rca.Loader.Configuration;

namespace Rca.Loader.Restart
{
    /// <summary>
    /// Manages the graceful restart of Revit when loader assemblies are updated.
    /// </summary>
    public class RestartManager
    {
        private static readonly ILogger Log = LoaderLog.GetLogger<RestartManager>();
        private readonly AssemblyStatusManager _statusManager;
        private const string ScriptFilename = "RestartRevitGraceful.ps1";
    // Build script path using current user profile to avoid hardcoding the username
    private static readonly string ScriptPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "rca-plugin", "build", "Scripts", ScriptFilename);

    public RestartManager(AssemblyStatusManager statusManager)
        {
            _statusManager = statusManager ?? throw new ArgumentNullException(nameof(statusManager));
        }

        public bool ShowRestartDialog(int countdownSeconds = 10)
        {
            try
            {
                Log.LogInformation("Showing restart dialog countdownSeconds={Seconds}", countdownSeconds);
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
                        var remaining = System.Threading.Interlocked.Decrement(ref countdown);
                        if (remaining >= 0)
                        {
                            taskDialog.MainContent = $"Revit needs to restart to load the updated assembly. The restart will begin in {remaining} seconds.\n\n" +
                                                     "Your work will be saved automatically before closing.\n\n" +
                                                     "Do you want to proceed with the restart?";
                        }
                    }
                    catch { }
                }, null, 0, 1000);

                var result = taskDialog.Show();
                timer.Dispose();
                Log.LogInformation("Restart dialog user selection={Result}", result);

                return result switch
                {
                    TaskDialogResult.CommandLink1 => ExecuteRestartScript(out _),
                    TaskDialogResult.CommandLink2 => false,
                    _ => false
                };
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "Error showing restart dialog");
                TaskDialog.Show("Error", $"An error occurred while showing the restart dialog: {ex.Message}");
                return false;
            }
        }

        public bool ExecuteRestartScript(out string error)
        {
            try
            {
                // No file copy anymore; MSBuild deploys to Addins/<timestamp>. We just restart Revit.

#if DEBUG
                // Use hardcoded script path (minimal fix)
                if (File.Exists(ScriptPath))
                {
                    ExecuteScript(ScriptPath, out error);
                    return string.IsNullOrEmpty(error);
                }
                error = $"Restart script not found at path: {ScriptPath}";
                return false;
#else
                error = "Restart functionality is only available in DEBUG builds";
                Log.LogInformation("Restart skipped - not available in RELEASE builds");
                return false;
#endif
            }
            catch (Exception ex)
            {
                error = $"Error executing restart script: {ex.Message}";
                Log.LogError(ex, "ExecuteRestartScript failed");
                return false;
            }
        }

        private void ExecuteScript(string scriptPath, out string error)
        {
            error = string.Empty;
            try
            {
                var revitProcess = Process.GetCurrentProcess();
                var revitExecutable = revitProcess.MainModule?.FileName ?? string.Empty;
                if (string.IsNullOrEmpty(revitExecutable)) 
                { 
                    error = "Could not determine Revit executable path"; 
                    Log.LogWarning("Revit executable path missing"); 
                    return; 
                }

#if DEBUG
                string? projectFilePath = null; // until settings are reintroduced
                var arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" -RevitExecutable \"{revitExecutable}\"";
                if (!string.IsNullOrWhiteSpace(projectFilePath))
                {
                    var expandedFilePath = PathExpander.ExpandPath(projectFilePath);
                    arguments += $" -FilePath \"{expandedFilePath}\"";
                    Log.LogInformation("Restart script will open Revit with file: {FilePath}", expandedFilePath);
                }
#else
                var arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" -RevitExecutable \"{revitExecutable}\"";
#endif

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(startInfo);
                if (process == null) 
                { 
                    error = "Failed to start PowerShell process"; 
                    Log.LogWarning("Failed to start powershell scriptPath={Script}", scriptPath); 
                }
                else 
                { 
                    Log.LogInformation("Restart script launched scriptPath={Script}", scriptPath); 
                }
            }
            catch (Exception ex)
            {
                error = $"Error executing script: {ex.Message}";
                Log.LogError(ex, "ExecuteScript failure scriptPath={Script}", scriptPath);
            }
        }

        /// <summary>
        /// Validates that all Loader and Runtime assemblies were copied successfully by comparing embedded source hashes.
        /// Every DLL in a group must share the same SourceHash value.
        /// </summary>
        public bool ValidateAssemblyCopy(string sourcePath, string targetPath)
        {
            try
            {
                bool loaderOk = ValidateGroup(sourcePath, targetPath, LoaderConstants.LoaderAssemblies);
                bool runtimeOk = ValidateGroup(sourcePath, targetPath, LoaderConstants.RuntimeAssemblies);
                return loaderOk && runtimeOk;
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "Error validating assembly copy source={Source} target={Target}", sourcePath, targetPath);
                return false;
            }
        }

        private static string GetShortHash(string hash)
        {
            if (string.IsNullOrEmpty(hash)) return string.Empty;
            var cleaned = hash.Trim();
            return cleaned.Length > 6 ? cleaned.Substring(0, 6) : cleaned;
        }

        private bool ValidateGroup(string sourcePath, string targetPath, string[] files)
        {
            string srcHash = string.Empty;
            foreach (var file in files)
            {
                var src = Path.Combine(sourcePath, file);
                var tgt = Path.Combine(targetPath, file);
                if (!File.Exists(tgt)) { Log.LogWarning("ValidateAssemblyCopy target missing path={Path}", tgt); return false; }

                var srcMetaHash = AttributeMetadataLoader.TryGetFromFile(src, BuildConstants.SourceHashMetadataKey);
                var tgtMetaHash = AttributeMetadataLoader.TryGetFromFile(tgt, BuildConstants.SourceHashMetadataKey);
                if (string.IsNullOrEmpty(srcMetaHash) || srcMetaHash == AttributeMetadataLoader.MissingMarker || string.IsNullOrEmpty(tgtMetaHash) || tgtMetaHash == AttributeMetadataLoader.MissingMarker)
                {
                    Log.LogWarning("ValidateAssemblyCopy missing metadata source={Src} target={Tgt}", srcMetaHash, tgtMetaHash);
                    return false;
                }

                if (string.IsNullOrEmpty(srcHash))
                {
                    srcHash = srcMetaHash;
                }
                else if (!string.Equals(GetShortHash(srcHash), GetShortHash(srcMetaHash), StringComparison.OrdinalIgnoreCase))
                {
                    Log.LogWarning("ValidateAssemblyCopy group mismatch src={Src} file={File}", GetShortHash(srcHash), file);
                    return false;
                }

                bool match = string.Equals(GetShortHash(srcMetaHash), GetShortHash(tgtMetaHash), StringComparison.OrdinalIgnoreCase);
                if (!match)
                {
                    Log.LogWarning("ValidateAssemblyCopy file mismatch file={File} src={Src} tgt={Tgt}", file, GetShortHash(srcMetaHash), GetShortHash(tgtMetaHash));
                    return false;
                }
            }
            return true;
        }
    }
}
