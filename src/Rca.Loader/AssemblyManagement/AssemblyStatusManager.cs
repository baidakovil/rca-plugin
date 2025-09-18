using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using Rca.Loader.Infrastructure;

namespace Rca.Loader.AssemblyManagement
{
    /// <summary>
    /// Manages the status of loaded assemblies, tracks changes, and maintains the JSON status file.
    /// </summary>
    /// <remarks>
    /// This class is responsible for:
    /// - Tracking which versions of assemblies are currently loaded
    /// - Detecting when newer versions are available
    /// - Updating the status display in the UI
    /// - Persisting assembly state between Revit sessions
    /// 
    /// Loader and Contracts assemblies are treated as a single logical unit since
    /// they are always updated and deployed together.
    /// </remarks>
    public class AssemblyStatusManager
    {
        private readonly string _jsonPath;
        private LoadedAssembliesInfo _currentInfo;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="AssemblyStatusManager"/> class.
        /// </summary>
        /// <param name="jsonPath">Optional path to the JSON file; defaults to LoaderConstants.LoadedAssembliesJsonPath.</param>
        public AssemblyStatusManager(string? jsonPath = null)
        {
            _jsonPath = jsonPath ?? LoaderConstants.LoadedAssembliesJsonPath;
            _currentInfo = new LoadedAssembliesInfo();
        }

        /// <summary>
        /// Gets the current assembly status information.
        /// </summary>
        public LoadedAssembliesInfo CurrentInfo => _currentInfo;
        
        /// <summary>
        /// Initializes the assembly status tracking system during startup.
        /// </summary>
        /// <remarks>
        /// This method:
        /// - Ensures required directories exist
        /// - Loads existing JSON state or creates initial state
        /// - Calculates hashes for currently loaded assemblies
        /// </remarks>
        public void InitializeOnStartup()
        {
            try
            {
                // Create directories if they don't exist
                EnsureDirectoriesExist();
                
                // Try to load existing info
                if (!LoadAssemblyInfo())
                {
                    // First run or JSON missing, set up initial paths
                    var paths = GetAssemblyPaths();
                    
                    // Set path to the directory containing loader components
                    _currentInfo.LoaderComponents.Path = paths.loaderDir;
                    _currentInfo.RuntimeAssembly.Path = paths.runtimePath;
                    
                    // Calculate initial hashes
                    _currentInfo.LoaderComponents.Hash = CalculateCombinedLoaderHash(paths.loaderPath, paths.contractsPath);
                    _currentInfo.RuntimeAssembly.Hash = CalculateHash(paths.runtimePath);
                    
                    // Save initial state
                    SaveAssemblyInfo(_currentInfo);
                }
            }
            catch (Exception ex)
            {
                // Log the error but continue - we don't want to prevent Revit from loading
                // just because our status tracking failed
                Debug.WriteLine($"Error initializing assembly status tracking: {ex.Message}");
            }
        }

        /// <summary>
        /// Calculates the SHA256 hash of a file.
        /// </summary>
        /// <param name="filePath">Path to the file to hash.</param>
        /// <returns>The hash as a hexadecimal string, or an empty string if the file doesn't exist.</returns>
        public string CalculateHash(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return string.Empty;
                }
                
                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(filePath);
                var hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error calculating hash for {filePath}: {ex.Message}");
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Calculates a combined hash for the Loader and Contracts assemblies.
        /// </summary>
        /// <param name="loaderPath">Path to the Loader assembly.</param>
        /// <param name="contractsPath">Path to the Contracts assembly.</param>
        /// <returns>A combined hash representing both assemblies, or an empty string if either file doesn't exist.</returns>
        /// <remarks>
        /// This method creates a deterministic combined hash that represents both assemblies as a single unit.
        /// The combined hash allows us to track changes to either assembly.
        /// </remarks>
        public string CalculateCombinedLoaderHash(string loaderPath, string contractsPath)
        {
            try
            {
                if (!File.Exists(loaderPath) || !File.Exists(contractsPath))
                {
                    return string.Empty;
                }
                
                var loaderHash = CalculateHash(loaderPath);
                var contractsHash = CalculateHash(contractsPath);
                
                if (string.IsNullOrEmpty(loaderHash) || string.IsNullOrEmpty(contractsHash))
                {
                    return string.Empty;
                }
                
                // Combine hashes in a deterministic way
                using var sha256 = SHA256.Create();
                var combinedBytes = Encoding.UTF8.GetBytes($"{loaderHash}:{contractsHash}");
                var combinedHash = sha256.ComputeHash(combinedBytes);
                return BitConverter.ToString(combinedHash).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error calculating combined hash: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Loads assembly information from the JSON file.
        /// </summary>
        /// <returns>True if the file was loaded successfully, false otherwise.</returns>
        public bool LoadAssemblyInfo()
        {
            try
            {
                if (!File.Exists(_jsonPath))
                {
                    return false;
                }
                
                var json = File.ReadAllText(_jsonPath);
                var info = JsonSerializer.Deserialize<LoadedAssembliesInfo>(json);
                
                if (info != null)
                {
                    _currentInfo = info;
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading assembly info from {_jsonPath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Saves assembly information to the JSON file.
        /// </summary>
        /// <param name="info">The assembly information to save.</param>
        public void SaveAssemblyInfo(LoadedAssembliesInfo info)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(info, options);
                
                // Ensure the directory exists
                var directory = Path.GetDirectoryName(_jsonPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                File.WriteAllText(_jsonPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving assembly info to {_jsonPath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the latest folder in the temporary DLL directory based on alphabetical sorting.
        /// </summary>
        /// <returns>The path to the latest folder, or an empty string if no folders are found.</returns>
        public string GetLatestTempDllFolder()
        {
            try
            {
                if (!Directory.Exists(LoaderConstants.TempDllFolder))
                {
                    return string.Empty;
                }
                
                var directories = Directory.GetDirectories(LoaderConstants.TempDllFolder);
                
                if (directories.Length == 0)
                {
                    return string.Empty;
                }
                
                // Sort alphabetically (which works well for timestamp-based folder names)
                // and return the last one (most recent)
                return directories.OrderBy(d => d).Last();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting latest temp DLL folder: {ex.Message}");
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Processes an MSBuild signal, checking for updated assemblies.
        /// </summary>
        /// <param name="tempDllPath">The path to the temporary DLL folder from the build.</param>
        public void ProcessMsBuildSignal(string tempDllPath)
        {
            try
            {
                // Find the latest assemblies
                var loaderPath = Path.Combine(tempDllPath, LoaderConstants.LoaderFileName);
                var contractsPath = Path.Combine(tempDllPath, LoaderConstants.LoaderContractsFileName);
                var runtimePath = Path.Combine(tempDllPath, LoaderConstants.RuntimeFileName);
                
                // Calculate hashes for new assemblies
                var loaderComponentsHash = CalculateCombinedLoaderHash(loaderPath, contractsPath);
                var runtimeHash = CalculateHash(runtimePath);
                
                // Check what has changed
                bool loaderComponentsChanged = !string.IsNullOrEmpty(loaderComponentsHash) && 
                                             loaderComponentsHash != _currentInfo.LoaderComponents.Hash;
                                       
                bool runtimeChanged = !string.IsNullOrEmpty(runtimeHash) && 
                                     runtimeHash != _currentInfo.RuntimeAssembly.Hash;
                
                // Update signal info
                UpdateSignalInfo(DetermineEventType(loaderComponentsChanged, runtimeChanged));
                
                // Update UI if available
                // The UI update would be handled by a separate component that observes this class
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing MSBuild signal: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines if the Loader components are outdated.
        /// </summary>
        /// <returns>True if the loader components are outdated, false otherwise.</returns>
        public bool IsLoaderOutdated()
        {
            try
            {
                var latestFolder = GetLatestTempDllFolder();
                if (string.IsNullOrEmpty(latestFolder))
                {
                    return false;
                }
                
                var loaderPath = Path.Combine(latestFolder, LoaderConstants.LoaderFileName);
                var contractsPath = Path.Combine(latestFolder, LoaderConstants.LoaderContractsFileName);
                
                var combinedHash = CalculateCombinedLoaderHash(loaderPath, contractsPath);
                
                return !string.IsNullOrEmpty(combinedHash) && combinedHash != _currentInfo.LoaderComponents.Hash;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking if loader is outdated: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Determines if the Runtime assembly is outdated.
        /// </summary>
        /// <returns>True if the assembly is outdated, false otherwise.</returns>
        public bool IsRuntimeOutdated()
        {
            try
            {
                var latestFolder = GetLatestTempDllFolder();
                if (string.IsNullOrEmpty(latestFolder))
                {
                    return false;
                }
                
                var runtimePath = Path.Combine(latestFolder, LoaderConstants.RuntimeFileName);
                var runtimeHash = CalculateHash(runtimePath);
                
                return !string.IsNullOrEmpty(runtimeHash) && runtimeHash != _currentInfo.RuntimeAssembly.Hash;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking if runtime is outdated: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Updates the assembly hashes and paths after a successful runtime reload.
        /// </summary>
        /// <param name="runtimePath">The path to the newly loaded Runtime assembly.</param>
        public void UpdateHashesAfterReload(string runtimePath)
        {
            try
            {
                if (string.IsNullOrEmpty(runtimePath) || !File.Exists(runtimePath))
                {
                    return;
                }
                
                // Update runtime path and hash
                _currentInfo.RuntimeAssembly.Path = runtimePath;
                _currentInfo.RuntimeAssembly.Hash = CalculateHash(runtimePath);
                
                // Save changes to JSON
                SaveAssemblyInfo(_currentInfo);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating hashes after reload: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Updates the loader components hashes and path after a successful loader restart.
        /// </summary>
        /// <param name="loaderDir">The directory containing the newly loaded loader components.</param>
        public void UpdateLoaderComponentsHashesAfterRestart(string loaderDir)
        {
            try
            {
                if (string.IsNullOrEmpty(loaderDir) || !Directory.Exists(loaderDir))
                {
                    return;
                }
                
                var loaderPath = Path.Combine(loaderDir, LoaderConstants.LoaderFileName);
                var contractsPath = Path.Combine(loaderDir, LoaderConstants.LoaderContractsFileName);
                
                if (!File.Exists(loaderPath) || !File.Exists(contractsPath))
                {
                    return;
                }
                
                // Update loader components path and combined hash
                _currentInfo.LoaderComponents.Path = loaderDir;
                _currentInfo.LoaderComponents.Hash = CalculateCombinedLoaderHash(loaderPath, contractsPath);
                
                // Save changes to JSON
                SaveAssemblyInfo(_currentInfo);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating loader components hashes after restart: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Determines the event type based on which assemblies have changed.
        /// </summary>
        /// <param name="loaderComponentsChanged">Whether the Loader components have changed.</param>
        /// <param name="runtimeChanged">Whether the Runtime assembly has changed.</param>
        /// <returns>A string describing the event type.</returns>
        public string DetermineEventType(bool loaderComponentsChanged, bool runtimeChanged)
        {
            if (loaderComponentsChanged && runtimeChanged)
            {
                return "both loader and runtime outdated";
            }
            else if (loaderComponentsChanged)
            {
                return "only loader outdated";
            }
            else if (runtimeChanged)
            {
                return "only runtime outdated";
            }
            else
            {
                return "no changes";
            }
        }
        
        /// <summary>
        /// Updates the signal information with the current time and event type.
        /// </summary>
        /// <param name="eventType">The event type to record.</param>
        public void UpdateSignalInfo(string eventType)
        {
            _currentInfo.LastMSBuildSignal.Time = DateTime.Now.ToString("HH:mm:ss");
            _currentInfo.LastMSBuildSignal.Event = eventType;
            SaveAssemblyInfo(_currentInfo);
        }
        
        /// <summary>
        /// Ensures that all required directories exist.
        /// </summary>
        private void EnsureDirectoriesExist()
        {
            try
            {
                var tempDllDir = LoaderConstants.TempDllFolder;
                if (!Directory.Exists(tempDllDir))
                {
                    Directory.CreateDirectory(tempDllDir);
                }
                
                var jsonDir = Path.GetDirectoryName(_jsonPath);
                if (!string.IsNullOrEmpty(jsonDir) && !Directory.Exists(jsonDir))
                {
                    Directory.CreateDirectory(jsonDir);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error ensuring directories exist: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Gets the paths to the currently loaded assemblies.
        /// </summary>
        /// <returns>A tuple containing the loader directory, loader path, contracts path, and runtime path.</returns>
        private (string loaderDir, string loaderPath, string contractsPath, string runtimePath) GetAssemblyPaths()
        {
            try
            {
                // Get the path to the Loader assembly (from executing assembly)
                var loaderAssembly = Assembly.GetExecutingAssembly();
                var loaderPath = loaderAssembly.Location;
                var loaderDir = Path.GetDirectoryName(loaderPath) ?? string.Empty;
                
                // Get the path to the Contracts assembly (should be in the same folder)
                var contractsPath = Path.Combine(loaderDir, LoaderConstants.LoaderContractsFileName);
                
                // Get the path to the Runtime assembly
                // First try to find it from the latest folder in TempDllFolder
                var latestFolder = GetLatestTempDllFolder();
                string runtimePath;
                
                if (!string.IsNullOrEmpty(latestFolder))
                {
                    runtimePath = Path.Combine(latestFolder, LoaderConstants.RuntimeFileName);
                    if (File.Exists(runtimePath))
                    {
                        return (loaderDir, loaderPath, contractsPath, runtimePath);
                    }
                }
                
                // If not found, use empty string
                runtimePath = string.Empty;
                
                return (loaderDir, loaderPath, contractsPath, runtimePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting assembly paths: {ex.Message}");
                return (string.Empty, string.Empty, string.Empty, string.Empty);
            }
        }
        
        /// <summary>
        /// Finds the latest versions of all assemblies in the temporary directory.
        /// </summary>
        /// <returns>A tuple containing AssemblyInfo objects for the latest assemblies.</returns>
        private (AssemblyInfo loaderComponents, AssemblyInfo runtime) FindLatestAssemblies()
        {
            try
            {
                var latestFolder = GetLatestTempDllFolder();
                if (string.IsNullOrEmpty(latestFolder))
                {
                    return (new AssemblyInfo(), new AssemblyInfo());
                }
                
                var loaderPath = Path.Combine(latestFolder, LoaderConstants.LoaderFileName);
                var contractsPath = Path.Combine(latestFolder, LoaderConstants.LoaderContractsFileName);
                var runtimePath = Path.Combine(latestFolder, LoaderConstants.RuntimeFileName);
                
                var loaderComponents = new AssemblyInfo
                {
                    Path = latestFolder, // Store directory path instead of file path
                    Hash = CalculateCombinedLoaderHash(loaderPath, contractsPath)
                };
                
                var runtime = new AssemblyInfo
                {
                    Path = runtimePath,
                    Hash = CalculateHash(runtimePath)
                };
                
                return (loaderComponents, runtime);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding latest assemblies: {ex.Message}");
                return (new AssemblyInfo(), new AssemblyInfo());
            }
        }
    }
}
