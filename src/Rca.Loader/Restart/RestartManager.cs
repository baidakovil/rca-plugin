using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.UI;
using Rca.Loader.AssemblyManagement;
using Rca.Loader.Infrastructure;
using Rca.Loader.Configuration;
using Rca.Loader.Logging;
using Microsoft.Extensions.Logging;

namespace Rca.Loader.Restart
{
    /// <summary>
    /// Manages the graceful restart of Revit when loader assemblies are updated.
    /// </summary>
    public class RestartManager
    {
        private static readonly ILogger Log = LoaderLog.GetLogger<RestartManager>();
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
                        var remaining = Interlocked.Decrement(ref countdown);
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

        /// <summary>
        /// Executes the PowerShell restart script.
        /// </summary>
        public bool ExecuteRestartScript(out string error)
        {
            try
            {
                var sourcePath = _statusManager.GetLatestTempDllFolder();
                if (string.IsNullOrEmpty(sourcePath)) 
                { 
                    error = "Source path not found"; 
                    Log.LogWarning("Restart script aborted: source path missing"); 
                    return false; 
                }
                
                var targetPath = LoaderConstants.RevitAddinDir;
                if (string.IsNullOrEmpty(targetPath) || !Directory.Exists(targetPath)) 
                { 
                    error = "Target path not found"; 
                    Log.LogWarning("Restart script aborted: target path invalid path={Path}", targetPath); 
                    return false; 
                }

#if DEBUG
                // In DEBUG builds, get script path from settings
                var settings = SettingsService.LoadSettings();
                var configuredPath = settings.Debug?.RestartScriptPath ?? string.Empty;
                
                if (!string.IsNullOrWhiteSpace(configuredPath))
                {
                    var expandedPath = PathExpander.ExpandPath(configuredPath);
                    Log.LogDebug("Configured restart script path: {Path} (expanded: {Expanded})", 
                        configuredPath, expandedPath);
                    
                    if (File.Exists(expandedPath))
                    {
                        Log.LogInformation("Using restart script from settings: {Path}", expandedPath);
                        ExecuteScript(expandedPath, sourcePath, targetPath, out error);
                        return string.IsNullOrEmpty(error);
                    }
                    else
                    {
                        Log.LogWarning("Configured restart script not found: {Path}", expandedPath);
                    }
                }
                
                error = $"Restart script not found at configured path: {configuredPath}";
                return false;
#else
                // In RELEASE builds, restart functionality is not available
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

        private void ExecuteScript(string scriptPath, string sourcePath, string targetPath, out string error)
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
                // Get project file path from settings if configured
                var settings = SettingsService.LoadSettings();
                var projectFilePath = settings.Debug?.RevitProjectFilePath;
                
                // Build PowerShell arguments
                var arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" -SourcePath \"{sourcePath}\" -TargetPath \"{targetPath}\" -RevitExecutable \"{revitExecutable}\"";
                
                // Add file path parameter if configured
                if (!string.IsNullOrWhiteSpace(projectFilePath))
                {
                    var expandedFilePath = PathExpander.ExpandPath(projectFilePath);
                    arguments += $" -FilePath \"{expandedFilePath}\"";
                    Log.LogInformation("Restart script will open Revit with file: {FilePath}", expandedFilePath);
                }
#else
                var arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" -SourcePath \"{sourcePath}\" -TargetPath \"{targetPath}\" -RevitExecutable \"{revitExecutable}\"";
#endif

                var startInfo = new ProcessStartInfo
                {
                    FileName = PowerShellPath,
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
        /// Validates that the loader assembly was copied successfully by comparing embedded source hashes
        /// (AssemblyMetadata SourceHash) only.
        /// </summary>
        public bool ValidateAssemblyCopy(string sourcePath, string targetPath)
        {
            try
            {
                var loaderFileName = LoaderConstants.LoaderFileName;
                var loaderSourcePath = Path.Combine(sourcePath, loaderFileName);
                var loaderTargetPath = Path.Combine(targetPath, loaderFileName);
                if (!File.Exists(loaderTargetPath)) { Log.LogWarning("ValidateAssemblyCopy target missing path={Path}", loaderTargetPath); return false; }

                var srcMetaHash = AttributeMetadataLoader.TryGetFromFile(loaderSourcePath, "SourceHash");
                var tgtMetaHash = AttributeMetadataLoader.TryGetFromFile(loaderTargetPath, "SourceHash");
                if (string.IsNullOrEmpty(srcMetaHash) || srcMetaHash == AttributeMetadataLoader.MissingMarker || string.IsNullOrEmpty(tgtMetaHash) || tgtMetaHash == AttributeMetadataLoader.MissingMarker)
                {
                    Log.LogWarning("ValidateAssemblyCopy missing metadata source={Src} target={Tgt}", srcMetaHash, tgtMetaHash);
                    return false;
                }
                var srcShort = GetShortHash(srcMetaHash);
                var tgtShort = GetShortHash(tgtMetaHash);
                bool match = string.Equals(srcShort, tgtShort, StringComparison.OrdinalIgnoreCase);
                if (!match) Log.LogWarning("ValidateAssemblyCopy mismatch src={Src} tgt={Tgt}", srcShort, tgtShort);
                return match;
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
    }
}
