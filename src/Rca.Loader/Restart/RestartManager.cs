using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="RestartManager"/> class.
        /// </summary>
        /// <param name="statusManager">The assembly status manager.</param>
        /// <exception cref="ArgumentNullException">Thrown if statusManager is null.</exception>
        public RestartManager(AssemblyStatusManager statusManager)
        {
            _statusManager = statusManager ?? throw new ArgumentNullException(nameof(statusManager));
        }

        /// <summary>
        /// Shows a dialog with restart options and countdown.
        /// </summary>
        /// <param name="countdownSeconds">Number of seconds for the countdown.</param>
        /// <returns>True if restart was initiated, false if cancelled.</returns>
        public bool ShowRestartDialog(int countdownSeconds = 10)
        {
            try
            {
                var taskDialog = new TaskDialog("Revit Restart Required")
                {
                    MainIcon = TaskDialogIcon.TaskDialogIconWarning,
                    MainInstruction = "Loader assemblies have been updated",
                    MainContent = $"Revit needs to restart to load the updated assemblies. The restart will begin in {countdownSeconds} seconds.\n\n" +
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
                            taskDialog.MainContent = $"Revit needs to restart to load the updated assemblies. The restart will begin in {remaining} seconds.\n\n" +
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
                
                // Clean up the timer
                timer.Dispose();
                
                switch (result)
                {
                    case TaskDialogResult.CommandLink1:
                        // User chose to restart now
                        return ExecuteRestartScript(out _);
                        
                    case TaskDialogResult.CommandLink2:
                        // User chose to restart later
                        TaskDialog.Show("Restart Later", 
                            "Please remember to restart Revit manually to load the updated assemblies.");
                        return false;
                        
                    default:
                        // User cancelled
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
        /// <param name="error">Output error message if execution fails.</param>
        /// <returns>True if the script was executed successfully, false otherwise.</returns>
        public bool ExecuteRestartScript(out string error)
        {
            try
            {
                // Get source path (latest build folder)
                string sourcePath = _statusManager.GetLatestTempDllFolder();
                if (string.IsNullOrEmpty(sourcePath))
                {
                    error = "Source path not found";
                    return false;
                }
                
                // Get target path (Revit addin directory)
                string targetPath = LoaderConstants.RevitAddinDir;
                if (string.IsNullOrEmpty(targetPath) || !Directory.Exists(targetPath))
                {
                    error = "Target path not found";
                    return false;
                }
                
                // Check if script exists
                if (!File.Exists(ScriptPath))
                {
                    // Try to find the script in alternate locations
                    var altPath = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "build", "Scripts", ScriptFilename);
                        
                    if (!File.Exists(altPath))
                    {
                        error = $"Restart script not found at: {ScriptPath}";
                        return false;
                    }
                    
                    // Use the alternate path
                    ExecuteScript(altPath, sourcePath, targetPath, out error);
                    return string.IsNullOrEmpty(error);
                }
                
                // Execute the script with appropriate parameters
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
                // Get Revit process info for script
                var revitProcess = Process.GetCurrentProcess();
                string revitExecutable = revitProcess.MainModule?.FileName ?? string.Empty;
                
                if (string.IsNullOrEmpty(revitExecutable))
                {
                    error = "Could not determine Revit executable path";
                    return;
                }
                
                // Set up PowerShell process
                var startInfo = new ProcessStartInfo
                {
                    FileName = PowerShellPath,
                    Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" " +
                               $"-SourcePath \"{sourcePath}\" " +
                               $"-TargetPath \"{targetPath}\" " +
                               $"-RevitExecutable \"{revitExecutable}\" " +
                               $"-JsonFilePath \"{LoaderConstants.LoadedAssembliesJsonPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                
                // Start the process
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    error = "Failed to start PowerShell process";
                    return;
                }
                
                // The script will handle closing Revit, so we don't need to wait for it to complete
            }
            catch (Exception ex)
            {
                error = $"Error executing script: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Validates that assemblies were copied successfully.
        /// </summary>
        /// <param name="sourcePath">Source directory path.</param>
        /// <param name="targetPath">Target directory path.</param>
        /// <returns>True if validation passed, false otherwise.</returns>
        public bool ValidateAssemblyCopy(string sourcePath, string targetPath)
        {
            try
            {
                // Check that both loader and contracts files exist in target
                var loaderSourcePath = Path.Combine(sourcePath, LoaderConstants.LoaderFileName);
                var contractsSourcePath = Path.Combine(sourcePath, LoaderConstants.LoaderContractsFileName);
                
                var loaderTargetPath = Path.Combine(targetPath, LoaderConstants.LoaderFileName);
                var contractsTargetPath = Path.Combine(targetPath, LoaderConstants.LoaderContractsFileName);
                
                if (!File.Exists(loaderTargetPath) || !File.Exists(contractsTargetPath))
                {
                    return false;
                }
                
                // Check that the hashes of copied files match the source files
                var loaderSourceHash = _statusManager.CalculateHash(loaderSourcePath);
                var contractsSourceHash = _statusManager.CalculateHash(contractsSourcePath);
                
                var loaderTargetHash = _statusManager.CalculateHash(loaderTargetPath);
                var contractsTargetHash = _statusManager.CalculateHash(contractsTargetPath);
                
                return loaderSourceHash == loaderTargetHash && 
                       contractsSourceHash == contractsTargetHash;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error validating assembly copy: {ex.Message}");
                return false;
            }
        }
    }
}
