using System;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
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
    /// The Loader assembly now includes the Contracts assembly merged into it.
    /// </remarks>
    public class AssemblyStatusManager
    {
        private readonly string _jsonPath;
        private LoadedAssembliesInfo _currentInfo;

        private const string LoaderSourceHashFile = "source-hash.loader.txt";
        private const string RuntimeSourceHashFile = "source-hash.runtime.txt";

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
                    
                    // Set path to the directory containing loader
                    _currentInfo.LoaderComponents.Path = paths.loaderDir;
                    _currentInfo.RuntimeAssembly.Path = paths.runtimePath;
                    
                    // Read loader hash from addon (addin folder) first
                    var loaderHash = ReadSourceHashFromDir(paths.loaderDir, LoaderSourceHashFile);

                    // Fallback: latest runtime folder
                    if (string.IsNullOrEmpty(loaderHash))
                    {
                        var latest = GetLatestTempDllFolder();
                        if (!string.IsNullOrEmpty(latest))
                            loaderHash = ReadSourceHashFromDir(latest, LoaderSourceHashFile);
                    }

                    // Fallback: search runtime root for most recent loader hash
                    if (string.IsNullOrEmpty(loaderHash))
                    {
                        loaderHash = ReadSourceHashFromRuntimeRoot(LoaderSourceHashFile);
                    }

                    _currentInfo.LoaderComponents.Hash = loaderHash ?? string.Empty;

                    // Read runtime hash from the runtime deploy folder
                    var runtimeHash = ReadSourceHashFromFile(paths.runtimePath, RuntimeSourceHashFile);

                    if (string.IsNullOrEmpty(runtimeHash))
                    {
                        var latest = GetLatestTempDllFolder();
                        if (!string.IsNullOrEmpty(latest))
                            runtimeHash = ReadSourceHashFromDir(latest, RuntimeSourceHashFile);
                    }

                    if (string.IsNullOrEmpty(runtimeHash))
                    {
                        runtimeHash = ReadSourceHashFromRuntimeRoot(RuntimeSourceHashFile);
                    }

                    _currentInfo.RuntimeAssembly.Hash = runtimeHash ?? string.Empty;

                    // Developer fallback: compute from repo root if still empty
                    if (string.IsNullOrEmpty(_currentInfo.LoaderComponents.Hash))
                    {
                        var repoRoot = FindRepoRoot(paths.loaderDir);
                        if (!string.IsNullOrEmpty(repoRoot))
                            _currentInfo.LoaderComponents.Hash = ComputeSourceHashFromRoot(repoRoot);
                    }

                    if (string.IsNullOrEmpty(_currentInfo.RuntimeAssembly.Hash))
                    {
                        var repoRoot = FindRepoRoot(paths.loaderDir);
                        if (!string.IsNullOrEmpty(repoRoot))
                            _currentInfo.RuntimeAssembly.Hash = ComputeSourceHashFromRoot(repoRoot);
                    }

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

        private string ReadSourceHashFromDir(string dir, string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(dir)) return string.Empty;
                var candidate = Path.Combine(dir, fileName);
                if (File.Exists(candidate))
                    return File.ReadAllText(candidate).Trim();
                return string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading source hash from dir {dir}: {ex.Message}");
                return string.Empty;
            }
        }

        private string ReadSourceHashFromFile(string filePath, string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath)) return string.Empty;
                var dir = Path.GetDirectoryName(filePath) ?? string.Empty;
                return ReadSourceHashFromDir(dir, fileName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading source hash from file {filePath}: {ex.Message}");
                return string.Empty;
            }
        }

        private string ReadSourceHashFromRuntimeRoot(string fileName)
        {
            try
            {
                var root = LoaderConstants.RuntimeDeployRoot;
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return string.Empty;

                // Find all source-hash.txt files under runtime root and pick the most recent by write time
                var files = Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories)
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(fi => fi.LastWriteTimeUtc)
                    .ToList();

                var file = files.FirstOrDefault();
                if (file != null && file.Exists)
                {
                    return File.ReadAllText(file.FullName).Trim();
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error searching runtime root for source hash: {ex.Message}");
                return string.Empty;
            }
        }

        private string FindRepoRoot(string startDir)
        {
            try
            {
                if (string.IsNullOrEmpty(startDir)) return string.Empty;
                var dir = new DirectoryInfo(startDir);
                while (dir != null)
                {
                    // look for .git folder or a solution file
                    if (Directory.Exists(Path.Combine(dir.FullName, ".git"))) return dir.FullName;
                    if (Directory.EnumerateFiles(dir.FullName, "*.sln").Any()) return dir.FullName;
                    if (Directory.Exists(Path.Combine(dir.FullName, "src"))) return dir.FullName;
                    dir = dir.Parent;
                }
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private string ComputeSourceHashFromRoot(string root)
        {
            try
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return string.Empty;
                var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".cs", ".csproj", ".props", ".targets", ".xaml", ".resx", ".json", ".tt", ".config", ".xml" };
                var ignoreDirs = new[] { "bin", "obj", ".git", ".vs", "node_modules", "packages" };

                var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                    .Where(f => exts.Contains(Path.GetExtension(f)))
                    .Where(f => !IsUnderIgnoredDir(f, root, ignoreDirs))
                    .OrderBy(f => Path.GetRelativePath(root, f), StringComparer.Ordinal)
                    .ToList();

                using var sha = SHA256.Create();
                foreach (var f in files)
                {
                    if (IsTextFile(f))
                    {
                        var text = File.ReadAllText(f);
                        text = text.Replace("\r\n", "\n").Replace("\r", "\n");
                        var bytes = Encoding.UTF8.GetBytes(text);
                        sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
                    }
                    else
                    {
                        var bytes = File.ReadAllBytes(f);
                        sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
                    }
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return BitConverter.ToString(sha.Hash!).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error computing source hash from root {root}: {ex.Message}");
                return string.Empty;
            }
        }

        private static bool IsUnderIgnoredDir(string filePath, string root, string[] ignoreDirs)
        {
            var rel = Path.GetRelativePath(root, filePath);
            var parts = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (var p in parts)
            {
                if (ignoreDirs.Contains(p, StringComparer.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool IsTextFile(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var textExt = new HashSet<string> { ".cs", ".csproj", ".props", ".targets", ".xaml", ".resx", ".json", ".tt", ".config", ".xml" };
            return textExt.Contains(ext);
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
        /// Gets the latest folder in the runtime deployment directory based on alphabetical sorting.
        /// </summary>
        /// <returns>The path to the latest folder, or an empty string if no folders are found.</returns>
        public string GetLatestTempDllFolder()
        {
            try
            {
                if (!Directory.Exists(LoaderConstants.RuntimeDeployRoot))
                {
                    return string.Empty;
                }
                
                var directories = Directory.GetDirectories(LoaderConstants.RuntimeDeployRoot);
                
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
                Debug.WriteLine($"Error getting latest runtime deployment folder: {ex.Message}");
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
                var loaderHash = ReadSourceHashFromDir(tempDllPath, LoaderSourceHashFile);
                var runtimeHash = ReadSourceHashFromDir(tempDllPath, RuntimeSourceHashFile);

                bool loaderComponentsChanged = !string.IsNullOrEmpty(loaderHash) && loaderHash != _currentInfo.LoaderComponents.Hash;
                bool runtimeChanged = !string.IsNullOrEmpty(runtimeHash) && runtimeHash != _currentInfo.RuntimeAssembly.Hash;

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
                
                var loaderHash = ReadSourceHashFromDir(latestFolder, LoaderSourceHashFile);
                if (string.IsNullOrEmpty(loaderHash)) loaderHash = ReadSourceHashFromRuntimeRoot(LoaderSourceHashFile);
                
                return !string.IsNullOrEmpty(loaderHash) && loaderHash != _currentInfo.LoaderComponents.Hash;
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
                
                var runtimeHash = ReadSourceHashFromDir(latestFolder, RuntimeSourceHashFile);
                if (string.IsNullOrEmpty(runtimeHash)) runtimeHash = ReadSourceHashFromRuntimeRoot(RuntimeSourceHashFile);
                
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
                _currentInfo.RuntimeAssembly.Hash = ReadSourceHashFromFile(runtimePath, RuntimeSourceHashFile);
                
                if (string.IsNullOrEmpty(_currentInfo.RuntimeAssembly.Hash))
                {
                    // try latest folder
                    var latest = GetLatestTempDllFolder();
                    if (!string.IsNullOrEmpty(latest))
                        _currentInfo.RuntimeAssembly.Hash = ReadSourceHashFromDir(latest, RuntimeSourceHashFile);
                }
                
                if (string.IsNullOrEmpty(_currentInfo.RuntimeAssembly.Hash))
                {
                    _currentInfo.RuntimeAssembly.Hash = ReadSourceHashFromRuntimeRoot(RuntimeSourceHashFile);
                }
                
                // Save changes to JSON
                SaveAssemblyInfo(_currentInfo);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating hashes after reload: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Updates the loader components hash and path after a successful loader restart.
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
                
                if (!File.Exists(loaderPath))
                {
                    return;
                }
                
                // Update loader components path and hash
                _currentInfo.LoaderComponents.Path = loaderDir;
                _currentInfo.LoaderComponents.Hash = ReadSourceHashFromDir(loaderDir, LoaderSourceHashFile);
                
                if (string.IsNullOrEmpty(_currentInfo.LoaderComponents.Hash))
                {
                    // try latest folder
                    var latest = GetLatestTempDllFolder();
                    if (!string.IsNullOrEmpty(latest))
                        _currentInfo.LoaderComponents.Hash = ReadSourceHashFromDir(latest, LoaderSourceHashFile);
                }
                
                if (string.IsNullOrEmpty(_currentInfo.LoaderComponents.Hash))
                {
                    _currentInfo.LoaderComponents.Hash = ReadSourceHashFromRuntimeRoot(LoaderSourceHashFile);
                }
                
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
                var runtimeDeployDir = LoaderConstants.RuntimeDeployRoot;
                if (!Directory.Exists(runtimeDeployDir))
                {
                    Directory.CreateDirectory(runtimeDeployDir);
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
        /// <returns>A tuple containing the loader directory, loader path, and runtime path.</returns>
        private (string loaderDir, string loaderPath, string runtimePath) GetAssemblyPaths()
        {
            try
            {
                // Get the path to the Loader assembly (from executing assembly)
                var loaderAssembly = Assembly.GetExecutingAssembly();
                var loaderPath = loaderAssembly.Location;
                var loaderDir = Path.GetDirectoryName(loaderPath) ?? string.Empty;
                
                // Get the path to the Runtime assembly
                // First try to find it from the latest folder in RuntimeDeployRoot
                var latestFolder = GetLatestTempDllFolder();
                string runtimePath;
                
                if (!string.IsNullOrEmpty(latestFolder))
                {
                    runtimePath = Path.Combine(latestFolder, LoaderConstants.RuntimeFileName);
                    if (File.Exists(runtimePath))
                    {
                        return (loaderDir, loaderPath, runtimePath);
                    }
                }
                
                // If not found, use empty string
                runtimePath = string.Empty;
                
                return (loaderDir, loaderPath, runtimePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting assembly paths: {ex.Message}");
                return (string.Empty, string.Empty, string.Empty);
            }
        }
        
        /// <summary>
        /// Finds the latest versions of all assemblies in the runtime deployment directory.
        /// </summary>
        /// <returns>A tuple containing AssemblyInfo objects for the latest assemblies.</returns>
        private (AssemblyInfo loaderComponents, AssemblyInfo runtime) FindLatestAssemblies()
        {
            try
            {
                var latestFolder = GetLatestTempDllFolder();
                if (string.IsNullOrEmpty(latestFolder)) return (new AssemblyInfo(), new AssemblyInfo());
                var loaderPath = Path.Combine(latestFolder, LoaderConstants.LoaderFileName);
                var runtimePath = Path.Combine(latestFolder, LoaderConstants.RuntimeFileName);
                var loaderComponents = new AssemblyInfo { Path = latestFolder, Hash = ReadSourceHashFromDir(latestFolder, LoaderSourceHashFile) };
                var runtime = new AssemblyInfo { Path = runtimePath, Hash = ReadSourceHashFromDir(latestFolder, RuntimeSourceHashFile) };
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
