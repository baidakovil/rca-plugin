using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using Rca.Loader.Infrastructure;
using Microsoft.Extensions.Logging;
using Rca.Loader.Logging;

namespace Rca.Loader.AssemblyManagement
{
    /// <summary>
    /// Tracks hash and path metadata for Loader and Runtime assemblies and exposes change state.
    /// Uses BuildConstants for consistent metadata key names and file patterns.
    /// </summary>
    public class AssemblyStatusManager
    {
        private readonly ILogger _log = LoaderLog.GetLogger<AssemblyStatusManager>();

        public AssemblyStatusManager()
        {
        }

        public LoadedAssembliesInfo CurrentInfo { get; } = new LoadedAssembliesInfo();

        public void InitializeOnStartup()
        {
            try
            {
                _log.LogInformation("Initializing assembly status manager");

                // Read loader hash from current executing assembly using BuildConstants
                var loaderAsm = Assembly.GetExecutingAssembly();
                var loaderHash = AttributeMetadataLoader.TryGetFromLoadedAssembly(loaderAsm, BuildConstants.SourceHashMetadataKey);
                var loaderDeployFolder = AttributeMetadataLoader.TryGetFromLoadedAssembly(loaderAsm, BuildConstants.DeployFolderMetadataKey);

                // Prefer DeployFolder metadata if present - this is the folder name embedded into the DLL at build time
                if (!string.IsNullOrEmpty(loaderDeployFolder) && loaderDeployFolder != AttributeMetadataLoader.MissingMarker)
                {
                    CurrentInfo.LoaderComponents.Path = loaderDeployFolder;
                }
                else
                {
                    // Use explicit marker so UI doesn't try to treat it as a path
                    CurrentInfo.LoaderComponents.Path = AttributeMetadataLoader.MissingMarker;
                }

                CurrentInfo.LoaderComponents.Hash = string.IsNullOrEmpty(loaderHash) ? AttributeMetadataLoader.MissingMarker : loaderHash;

                // Determine runtime - prefer actually loaded runtime if available
                string? runtimePathFromRuntimeManager = LoaderApp.Instance?.RuntimeManager?.CurrentRuntimePath;
                if (!string.IsNullOrEmpty(runtimePathFromRuntimeManager) && File.Exists(runtimePathFromRuntimeManager))
                {
                    // Runtime is actually loaded - read its hash 
                    var loadedRuntimeAsm = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => !a.IsDynamic && string.Equals(Path.GetFileName(a.Location), LoaderConstants.RuntimeFileName, StringComparison.OrdinalIgnoreCase));

                    string? rHash = null;
                    if (loadedRuntimeAsm != null)
                    {
                        rHash = AttributeMetadataLoader.TryGetFromLoadedAssembly(loadedRuntimeAsm, BuildConstants.SourceHashMetadataKey);

                        // If reflection didn't return a usable value, try reading from the on-disk runtime DLL
                        if (string.IsNullOrEmpty(rHash) || rHash == AttributeMetadataLoader.MissingMarker)
                        {
                            try
                            {
                                if (File.Exists(runtimePathFromRuntimeManager))
                                {
                                    var fileHash = AttributeMetadataLoader.TryGetFromFile(runtimePathFromRuntimeManager, BuildConstants.SourceHashMetadataKey);
                                    if (!string.IsNullOrEmpty(fileHash) && fileHash != AttributeMetadataLoader.MissingMarker)
                                        rHash = fileHash;
                                }
                            }
                            catch (Exception exF)
                            {
                                _log.LogDebug(exF, "Failed secondary runtime hash read");
                            }
                        }
                    }
                    else
                    {
                        rHash = AttributeMetadataLoader.TryGetFromFile(runtimePathFromRuntimeManager, BuildConstants.SourceHashMetadataKey);
                    }

                    var finalHash = string.IsNullOrEmpty(rHash) ? AttributeMetadataLoader.MissingMarker : rHash;
                    
                    // Update LOADED runtime info (this is what's actually in memory)
                    CurrentInfo.LoadedRuntimeAssembly.Path = runtimePathFromRuntimeManager;
                    CurrentInfo.LoadedRuntimeAssembly.Hash = finalHash;
                    
                    // Also initialize discovered runtime to same values initially
                    CurrentInfo.RuntimeAssembly.Path = runtimePathFromRuntimeManager;
                    CurrentInfo.RuntimeAssembly.Hash = finalHash;
                }
                else
                {
                    // Determine latest runtime folder and read runtime hash directly from DLL metadata
                    var latest = GetLatestTempDllFolder();
                    if (!string.IsNullOrEmpty(latest))
                    {
                        var runtimeDll = Path.Combine(latest, LoaderConstants.RuntimeFileName);
                        if (File.Exists(runtimeDll))
                        {
                            var hash = AttributeMetadataLoader.TryGetFromFile(runtimeDll, BuildConstants.SourceHashMetadataKey);
                            var finalHash = string.IsNullOrEmpty(hash) ? AttributeMetadataLoader.MissingMarker : hash;
                            
                            // Set discovered runtime (what's on disk)
                            CurrentInfo.RuntimeAssembly.Path = runtimeDll;
                            CurrentInfo.RuntimeAssembly.Hash = finalHash;
                            
                            // Loaded runtime is empty (nothing loaded yet)
                            CurrentInfo.LoadedRuntimeAssembly.Path = string.Empty;
                            CurrentInfo.LoadedRuntimeAssembly.Hash = AttributeMetadataLoader.MissingMarker;
                        }
                        else
                        {
                            CurrentInfo.RuntimeAssembly.Path = string.Empty;
                            CurrentInfo.RuntimeAssembly.Hash = AttributeMetadataLoader.MissingMarker;
                            CurrentInfo.LoadedRuntimeAssembly.Path = string.Empty;
                            CurrentInfo.LoadedRuntimeAssembly.Hash = AttributeMetadataLoader.MissingMarker;
                        }
                    }
                    else
                    {
                        CurrentInfo.RuntimeAssembly.Path = string.Empty;
                        CurrentInfo.RuntimeAssembly.Hash = AttributeMetadataLoader.MissingMarker;
                        CurrentInfo.LoadedRuntimeAssembly.Path = string.Empty;
                        CurrentInfo.LoadedRuntimeAssembly.Hash = AttributeMetadataLoader.MissingMarker;
                    }
                }

                // Refresh UI to reflect initial values
                try { LoaderApp.Instance?.UpdateStatusDisplay(); } catch { }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error initializing assembly status");
            }
        }

        /// <summary>
        /// Reads hash value from version file using the specified pattern.
        /// Version file format: SourceHash-{Component}-{hash}.txt containing the hash value.
        /// </summary>
        /// <param name="dir">Directory to search for version file.</param>
        /// <param name="pattern">File pattern to match (e.g., BuildConstants.LoaderHashFilePattern).</param>
        /// <returns>Hash value from file, or null if not found.</returns>
        private string? ReadHashFromVersionFile(string dir, string pattern)
        {
            try
            {
                var files = Directory.GetFiles(dir, pattern);
                var file = files.FirstOrDefault();
                if (file != null) return File.ReadAllText(file).Trim();
                return null;
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "Error reading version file in {Dir}", dir);
                return null;
            }
        }

        public string GetLatestTempDllFolder()
        {
            try
            {
                if (!Directory.Exists(LoaderConstants.RuntimeDeployRoot)) return string.Empty;
                var dirs = Directory.GetDirectories(LoaderConstants.RuntimeDeployRoot);
                if (dirs.Length == 0) return string.Empty;
                // Sort descending by folder name and pick first - deterministic for timestamped folder names
                return dirs.OrderByDescending(d => Path.GetFileName(d)).First();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Error getting latest temp dll folder");
                return string.Empty;
            }
        }

        public void UpdateHashesAfterReload(string runtimePath)
        {
            try
            {
                if (string.IsNullOrEmpty(runtimePath) || !File.Exists(runtimePath)) return;
                
                // Read hash from the newly loaded runtime
                var hash = AttributeMetadataLoader.TryGetFromFile(runtimePath, BuildConstants.SourceHashMetadataKey);
                var newHash = string.IsNullOrEmpty(hash) ? AttributeMetadataLoader.MissingMarker : hash;
                
                var oldLoadedHash = CurrentInfo.LoadedRuntimeAssembly.Hash;
                
                // Update LOADED runtime info (this is what's actually in memory now)
                CurrentInfo.LoadedRuntimeAssembly.Path = runtimePath;
                CurrentInfo.LoadedRuntimeAssembly.Hash = newHash;
                
                // Also update discovered runtime info to match (since we just loaded it)
                CurrentInfo.RuntimeAssembly.Path = runtimePath;
                CurrentInfo.RuntimeAssembly.Hash = newHash;
                
                _log.LogInformation("Runtime hash updated after reload oldLoaded={OldHash} newLoaded={NewHash}", 
                    oldLoadedHash, newHash);
                
                // Refresh UI after reload
                try { LoaderApp.Instance?.UpdateStatusDisplay(); } catch { }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error updating hashes after reload");
            }
        }

        public void ProcessMsBuildSignal(string tempDllPath)
        {
            try
            {
                // Prefer the provided path, but fall back to latest
                var latest = !string.IsNullOrEmpty(tempDllPath) && Directory.Exists(tempDllPath)
                    ? tempDllPath
                    : GetLatestTempDllFolder();

                _log.LogDebug("ProcessMsBuildSignal tempDllPath={Temp} resolved={Resolved}", tempDllPath, latest);

                if (!string.IsNullOrEmpty(latest))
                {
                    // Read hashes from the actual DLL metadata (using BuildConstants for metadata keys)
                    string? loaderHash = null;
                    string? runtimeHash = null;

                    // Runtime hash - from runtime DLL in the folder
                    var runtimeDll = Path.Combine(latest, LoaderConstants.RuntimeFileName);
                    if (File.Exists(runtimeDll))
                    {
                        runtimeHash = AttributeMetadataLoader.TryGetFromFile(runtimeDll, BuildConstants.SourceHashMetadataKey);
                        _log.LogDebug("Runtime hash candidate {Hash} from {Dll}", runtimeHash, runtimeDll);
                    }

                    // Loader hash - try loader DLL in the provided folder; if not present, fall back to deployed loader path
                    var loaderDllInTemp = Path.Combine(latest, LoaderConstants.LoaderFileName);
                    string? deployFolderMeta = null;
                    if (File.Exists(loaderDllInTemp))
                    {
                        loaderHash = AttributeMetadataLoader.TryGetFromFile(loaderDllInTemp, BuildConstants.SourceHashMetadataKey);
                        deployFolderMeta = AttributeMetadataLoader.TryGetFromFile(loaderDllInTemp, BuildConstants.DeployFolderMetadataKey);
                        _log.LogDebug("Loader hash candidate {Hash} deployMeta={Deploy} from {Dll}", loaderHash, deployFolderMeta, loaderDllInTemp);
                    }
                    else if (File.Exists(LoaderConstants.LoaderAssemblyPath))
                    {
                        loaderHash = AttributeMetadataLoader.TryGetFromFile(LoaderConstants.LoaderAssemblyPath, BuildConstants.SourceHashMetadataKey);
                        deployFolderMeta = AttributeMetadataLoader.TryGetFromFile(LoaderConstants.LoaderAssemblyPath, BuildConstants.DeployFolderMetadataKey);
                        _log.LogDebug("Loader deployed hash candidate {Hash} deployMeta={Deploy}", loaderHash, deployFolderMeta);
                    }

                    // Determine what changed BEFORE updating CurrentInfo
                    bool loaderChanged = !string.IsNullOrEmpty(loaderHash) && loaderHash != CurrentInfo.LoaderComponents.Hash;
                    bool runtimeChanged = !string.IsNullOrEmpty(runtimeHash) && runtimeHash != CurrentInfo.RuntimeAssembly.Hash;

                    var ev = DetermineEventType(loaderChanged, runtimeChanged);
                    
                    // Record event in LastMSBuildSignal
                    var oldSignal = $"{CurrentInfo.LastMSBuildSignal.Time} - {CurrentInfo.LastMSBuildSignal.Event}";
                    CurrentInfo.LastMSBuildSignal.Time = DateTime.Now.ToString("HH:mm:ss");
                    CurrentInfo.LastMSBuildSignal.Event = ev;
                    _log.LogInformation("MSBuild signal processed prev={Prev} new={NewTime} {Event}", oldSignal, CurrentInfo.LastMSBuildSignal.Time, ev);

                    // CRITICAL FIX: Update CurrentInfo with new hashes and paths
                    // This must happen AFTER determining what changed, but BEFORE UI refresh
                    // so that IsLoaderOutdated() and IsRuntimeOutdated() see the latest values
                    
                    // Update loader info if we found a new hash
                    if (!string.IsNullOrEmpty(loaderHash))
                    {
                        var oldLoaderHash = CurrentInfo.LoaderComponents.Hash;
                        CurrentInfo.LoaderComponents.Hash = loaderHash;
                        
                        // Update deploy folder metadata for UI display
                        if (!string.IsNullOrEmpty(deployFolderMeta) && deployFolderMeta != AttributeMetadataLoader.MissingMarker)
                        {
                            CurrentInfo.LoaderComponents.Path = deployFolderMeta;
                        }
                        
                        _log.LogDebug("Loader info updated: hash={OldHash}->{NewHash} path={Path}", 
                            oldLoaderHash, loaderHash, deployFolderMeta);
                    }
                    
                    // Update runtime info if we found a new hash
                    if (!string.IsNullOrEmpty(runtimeHash))
                    {
                        var oldRuntimeHash = CurrentInfo.RuntimeAssembly.Hash;
                        CurrentInfo.RuntimeAssembly.Hash = runtimeHash;
                        CurrentInfo.RuntimeAssembly.Path = runtimeDll;
                        
                        _log.LogDebug("Runtime info updated: hash={OldHash}->{NewHash} path={Path}", 
                            oldRuntimeHash, runtimeHash, runtimeDll);
                    }

                    // Refresh UI after processing MSBuild signal
                    try { LoaderApp.Instance?.UpdateStatusDisplay(); } catch { }
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error processing msbuild signal");
            }
        }

        public bool IsLoaderOutdated()
        {
            try
            {
                // Try to read the deployed loader's metadata
                if (File.Exists(LoaderConstants.LoaderAssemblyPath))
                {
                    var loaderHash = AttributeMetadataLoader.TryGetFromFile(LoaderConstants.LoaderAssemblyPath, BuildConstants.SourceHashMetadataKey);
                    return !string.IsNullOrEmpty(loaderHash) && loaderHash != CurrentInfo.LoaderComponents.Hash;
                }

                // If deployed loader not found, conservatively assume not outdated
                return false;
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "IsLoaderOutdated check failed");
                return false;
            }
        }

        public bool IsRuntimeOutdated()
        {
            try
            {
                // If runtime isn't loaded into the process, treat as outdated so reload will occur
                var runtimeLoaded = LoaderApp.Instance?.RuntimeManager?.IsRuntimeLoaded ?? false;
                if (!runtimeLoaded) return true;

                // Get the latest deploy folder
                var latest = GetLatestTempDllFolder();
                if (string.IsNullOrEmpty(latest)) return false;

                var runtimeDll = Path.Combine(latest, LoaderConstants.RuntimeFileName);
                if (!File.Exists(runtimeDll)) return false;

                // Read hash from the DISCOVERED runtime (on disk)
                var discoveredHash = AttributeMetadataLoader.TryGetFromFile(runtimeDll, BuildConstants.SourceHashMetadataKey);

                // CRITICAL FIX: Compare discovered hash with LOADED hash, not with RuntimeAssembly.Hash
                // RuntimeAssembly.Hash was updated by ProcessMsBuildSignal, so comparing with it always returns false
                // LoadedRuntimeAssembly.Hash is updated only after actual reload, so it represents what's in memory
                var loadedHash = CurrentInfo.LoadedRuntimeAssembly.Hash;
                
                // If we don't have a recorded hash for currently loaded runtime, consider it outdated
                if (string.IsNullOrEmpty(loadedHash) || loadedHash == AttributeMetadataLoader.MissingMarker)
                {
                    _log.LogDebug("IsRuntimeOutdated: no loaded hash recorded, treating as outdated");
                    return true;
                }

                var isOutdated = !string.IsNullOrEmpty(discoveredHash) && discoveredHash != loadedHash;
                
                _log.LogDebug("IsRuntimeOutdated: discovered={Discovered} loaded={Loaded} result={Outdated}",
                    discoveredHash, loadedHash, isOutdated);
                
                return isOutdated;
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "IsRuntimeOutdated check failed");
                return false;
            }
        }

        public void UpdateLoaderComponentsHashesAfterRestart(string loaderDir)
        {
            try
            {
                if (string.IsNullOrEmpty(loaderDir) || !Directory.Exists(loaderDir)) return;

                var loaderDll = Path.Combine(loaderDir, LoaderConstants.LoaderFileName);

                // Prefer DeployFolder metadata when updating the displayed path
                if (File.Exists(loaderDll))
                {
                    var deployFolderMeta = AttributeMetadataLoader.TryGetFromFile(loaderDll, BuildConstants.DeployFolderMetadataKey);
                    CurrentInfo.LoaderComponents.Path = !string.IsNullOrEmpty(deployFolderMeta) && deployFolderMeta != AttributeMetadataLoader.MissingMarker ? deployFolderMeta : AttributeMetadataLoader.MissingMarker;

                    var hash = AttributeMetadataLoader.TryGetFromFile(loaderDll, BuildConstants.SourceHashMetadataKey);
                    CurrentInfo.LoaderComponents.Hash = string.IsNullOrEmpty(hash) ? AttributeMetadataLoader.MissingMarker : hash;
                }
                else
                {
                    CurrentInfo.LoaderComponents.Path = AttributeMetadataLoader.MissingMarker;
                    CurrentInfo.LoaderComponents.Hash = AttributeMetadataLoader.MissingMarker;
                }

                // Refresh UI after loader restart
                try { LoaderApp.Instance?.UpdateStatusDisplay(); } catch { }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error updating loader hashes after restart");
            }
        }

        public string DetermineEventType(bool loaderComponentsChanged, bool runtimeChanged)
        {
            if (loaderComponentsChanged && runtimeChanged) return "both loader and runtime outdated";
            if (loaderComponentsChanged) return "only loader outdated";
            if (runtimeChanged) return "only runtime outdated";
            return "no changes";
        }
    }
}
