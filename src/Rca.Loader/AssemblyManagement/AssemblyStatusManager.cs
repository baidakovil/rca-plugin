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
    /// IMPORTANT: Metadata is ALWAYS read from files on disk, never from already loaded assemblies.
    /// </summary>
    public class AssemblyStatusManager
    {
        private readonly ILogger _log = LoaderLog.GetLogger<AssemblyStatusManager>();

        public AssemblyStatusManager() { }

        public LoadedAssembliesInfo CurrentInfo { get; } = new LoadedAssembliesInfo();

        /// <summary>
        /// Initializes status at Loader startup. Reads metadata only from disk.
        /// </summary>
        public void InitializeOnStartup()
        {
            try
            {
                _log.LogInformation("Initializing assembly status manager (disk-only metadata)");

                // Loader: read from deployed Loader assembly on disk (addin dir)
                if (File.Exists(LoaderConstants.LoaderAssemblyPath))
                {
                    var deployFolder = AttributeMetadataLoader.TryGetFromFile(LoaderConstants.LoaderAssemblyPath, BuildConstants.DeployFolderMetadataKey);
                    var loaderHash = AttributeMetadataLoader.TryGetFromFile(LoaderConstants.LoaderAssemblyPath, BuildConstants.SourceHashMetadataKey);
                    _log.LogDebug("Loader metadata from disk path={Path} deploy={Deploy} hash={Hash}", LoaderConstants.LoaderAssemblyPath, deployFolder, loaderHash);

                    CurrentInfo.LoaderComponents.Path = string.IsNullOrEmpty(deployFolder) || deployFolder == AttributeMetadataLoader.MissingMarker ? AttributeMetadataLoader.MissingMarker : deployFolder;
                    CurrentInfo.LoaderComponents.Hash = string.IsNullOrEmpty(loaderHash) || loaderHash == AttributeMetadataLoader.MissingMarker ? AttributeMetadataLoader.MissingMarker : loaderHash;
                }
                else
                {
                    _log.LogWarning("Loader assembly not found at {Path}", LoaderConstants.LoaderAssemblyPath);
                    CurrentInfo.LoaderComponents.Path = AttributeMetadataLoader.MissingMarker;
                    CurrentInfo.LoaderComponents.Hash = AttributeMetadataLoader.MissingMarker;
                }

                // Runtime: determine latest runtime folder and compute group hash from disk
                var latest = GetLatestTempDllFolder();
                if (!string.IsNullOrEmpty(latest))
                {
                    var (ok, groupHash, reason) = TryReadRuntimeGroupHash(latest);
                    if (ok)
                    {
                        CurrentInfo.RuntimeAssembly.Path = Path.Combine(latest, LoaderConstants.RuntimeFileName);
                        CurrentInfo.RuntimeAssembly.Hash = groupHash;
                        _log.LogDebug("Runtime discovered from disk path={Path} hash={Hash}", CurrentInfo.RuntimeAssembly.Path, groupHash);
                    }
                    else
                    {
                        _log.LogWarning("Runtime group invalid in {Dir}: {Reason}", latest, reason);
                        CurrentInfo.RuntimeAssembly.Path = string.Empty;
                        CurrentInfo.RuntimeAssembly.Hash = AttributeMetadataLoader.MissingMarker;
                    }

                    // Loaded runtime is empty at startup
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

                try { LoaderApp.Instance?.UpdateStatusDisplay(); } catch { }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error initializing assembly status");
            }
        }

        private (bool Ok, string Hash, string? Reason) TryReadRuntimeGroupHash(string folder)
        {
            try
            {
                var hashes = new List<string>();
                foreach (var dll in LoaderConstants.RuntimeAssemblies)
                {
                    var path = Path.Combine(folder, dll);
                    if (!File.Exists(path))
                        return (false, AttributeMetadataLoader.MissingMarker, $"Missing runtime assembly: {dll}");
                    var hash = AttributeMetadataLoader.TryGetFromFile(path, BuildConstants.SourceHashMetadataKey);
                    _log.LogTrace("Runtime group hash read {Dll} -> {Hash}", dll, hash);
                    if (string.IsNullOrEmpty(hash) || hash == AttributeMetadataLoader.MissingMarker)
                        return (false, AttributeMetadataLoader.MissingMarker, $"Missing or empty SourceHash in {dll}");
                    hashes.Add(hash);
                }
                var shortHashes = hashes.Select(h => h.Trim()).Select(h => h.Length > 8 ? h[..8] : h).ToList();
                var allEqual = shortHashes.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1;
                if (!allEqual)
                    return (false, AttributeMetadataLoader.MissingMarker, $"Group hash mismatch: {string.Join(", ", shortHashes)}");
                return (true, hashes.First(), null);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error reading runtime group hash from {Dir}", folder);
                return (false, AttributeMetadataLoader.MissingMarker, ex.Message);
            }
        }

        private (bool Ok, string Hash, string? Reason) TryReadLoaderGroupHash(string loaderDir)
        {
            try
            {
                var hashes = new List<string>();
                foreach (var dll in LoaderConstants.LoaderAssemblies)
                {
                    var path = Path.Combine(loaderDir, dll);
                    if (!File.Exists(path))
                        return (false, AttributeMetadataLoader.MissingMarker, $"Missing loader assembly: {dll}");
                    var hash = AttributeMetadataLoader.TryGetFromFile(path, BuildConstants.SourceHashMetadataKey);
                    _log.LogTrace("Loader group hash read {Dll} -> {Hash}", dll, hash);
                    if (string.IsNullOrEmpty(hash) || hash == AttributeMetadataLoader.MissingMarker)
                        return (false, AttributeMetadataLoader.MissingMarker, $"Missing or empty SourceHash in {dll}");
                    hashes.Add(hash);
                }
                var shortHashes = hashes.Select(h => h.Trim()).Select(h => h.Length > 8 ? h[..8] : h).ToList();
                var allEqual = shortHashes.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1;
                if (!allEqual)
                    return (false, AttributeMetadataLoader.MissingMarker, $"Group hash mismatch: {string.Join(", ", shortHashes)}");
                return (true, hashes.First(), null);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error reading loader group hash from {Dir}", loaderDir);
                return (false, AttributeMetadataLoader.MissingMarker, ex.Message);
            }
        }

        public string GetLatestTempDllFolder()
        {
            try
            {
                if (!Directory.Exists(LoaderConstants.RuntimeDeployRoot)) return string.Empty;
                var dirs = Directory.GetDirectories(LoaderConstants.RuntimeDeployRoot);
                if (dirs.Length == 0) return string.Empty;
                return dirs.OrderByDescending(d => Path.GetFileName(d)).First();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Error getting latest temp dll folder");
                return string.Empty;
            }
        }

        /// <summary>
        /// Called after successful runtime reload. Reads group hash from disk and updates LOADED state.
        /// </summary>
        public void UpdateHashesAfterReload(string runtimePath)
        {
            try
            {
                if (string.IsNullOrEmpty(runtimePath) || !File.Exists(runtimePath)) return;
                var folder = Path.GetDirectoryName(runtimePath) ?? string.Empty;
                var (ok, hash, reason) = TryReadRuntimeGroupHash(folder);
                var finalHash = ok ? hash : AttributeMetadataLoader.MissingMarker;

                var oldLoadedHash = CurrentInfo.LoadedRuntimeAssembly.Hash;
                CurrentInfo.LoadedRuntimeAssembly.Path = runtimePath;
                CurrentInfo.LoadedRuntimeAssembly.Hash = finalHash;
                CurrentInfo.RuntimeAssembly.Path = runtimePath;
                CurrentInfo.RuntimeAssembly.Hash = finalHash;

                _log.LogInformation("Runtime hash updated after reload oldLoaded={OldHash} newLoaded={NewHash}", oldLoadedHash, finalHash);
                try { LoaderApp.Instance?.UpdateStatusDisplay(); } catch { }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error updating hashes after reload");
            }
        }

        /// <summary>
        /// Processes signal from MSBuild that a new build is available. No path is required; we resolve latest folder.
        /// </summary>
        public void ProcessMsBuildSignal(string tempDllPath)
        {
            try
            {
                var latest = !string.IsNullOrEmpty(tempDllPath) && Directory.Exists(tempDllPath) ? tempDllPath : GetLatestTempDllFolder();
                _log.LogDebug("ProcessMsBuildSignal tempDllPath={Temp} resolved={Resolved}", tempDllPath, latest);
                if (string.IsNullOrEmpty(latest)) return;

                // Read runtime group hash from disk
                var (rok, rHash, rReason) = TryReadRuntimeGroupHash(latest);
                if (rok)
                {
                    var oldRuntimeHash = CurrentInfo.RuntimeAssembly.Hash;
                    CurrentInfo.RuntimeAssembly.Hash = rHash;
                    CurrentInfo.RuntimeAssembly.Path = Path.Combine(latest, LoaderConstants.RuntimeFileName);
                    _log.LogDebug("Runtime info updated: hash={Old}->{New} path={Path}", oldRuntimeHash, rHash, CurrentInfo.RuntimeAssembly.Path);
                }
                else
                {
                    _log.LogWarning("Runtime group invalid in {Dir}: {Reason}", latest, rReason);
                }

                string? oldLoaderHashSnapshot = CurrentInfo.LoaderComponents.Hash;
                // Read loader metadata from deployed loader (addin dir)
                if (File.Exists(LoaderConstants.LoaderAssemblyPath))
                {
                    var lHash = AttributeMetadataLoader.TryGetFromFile(LoaderConstants.LoaderAssemblyPath, BuildConstants.SourceHashMetadataKey);
                    var lDeploy = AttributeMetadataLoader.TryGetFromFile(LoaderConstants.LoaderAssemblyPath, BuildConstants.DeployFolderMetadataKey);
                    if (!string.IsNullOrEmpty(lHash) && lHash != AttributeMetadataLoader.MissingMarker)
                    {
                        CurrentInfo.LoaderComponents.Hash = lHash;
                        if (!string.IsNullOrEmpty(lDeploy) && lDeploy != AttributeMetadataLoader.MissingMarker)
                            CurrentInfo.LoaderComponents.Path = lDeploy;
                        _log.LogDebug("Loader info updated: hash={Old}->{New} deploy={Deploy}", oldLoaderHashSnapshot, lHash, lDeploy);
                    }
                }

                bool loaderChanged = !string.Equals(oldLoaderHashSnapshot, CurrentInfo.LoaderComponents.Hash, StringComparison.OrdinalIgnoreCase);
                bool runtimeChanged = !string.IsNullOrEmpty(CurrentInfo.LoadedRuntimeAssembly.Hash) && !string.Equals(CurrentInfo.RuntimeAssembly.Hash, CurrentInfo.LoadedRuntimeAssembly.Hash, StringComparison.OrdinalIgnoreCase);
                var ev = DetermineEventType(loaderChanged, runtimeChanged);

                var oldSignal = $"{CurrentInfo.LastMSBuildSignal.Time} - {CurrentInfo.LastMSBuildSignal.Event}";
                CurrentInfo.LastMSBuildSignal.Time = DateTime.Now.ToString("HH:mm:ss");
                CurrentInfo.LastMSBuildSignal.Event = ev;
                _log.LogInformation("MSBuild signal processed prev={Prev} new={NewTime} {Event}", oldSignal, CurrentInfo.LastMSBuildSignal.Time, ev);

                try { LoaderApp.Instance?.UpdateStatusDisplay(); } catch { }
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
                if (!File.Exists(LoaderConstants.LoaderAssemblyPath)) return false;
                var loaderHash = AttributeMetadataLoader.TryGetFromFile(LoaderConstants.LoaderAssemblyPath, BuildConstants.SourceHashMetadataKey);
                return !string.IsNullOrEmpty(loaderHash) && loaderHash != CurrentInfo.LoaderComponents.Hash;
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
                var runtimeLoaded = LoaderApp.Instance?.RuntimeManager?.IsRuntimeLoaded ?? false;
                if (!runtimeLoaded) { _log.LogTrace("IsRuntimeOutdated: runtime not loaded -> true"); return true; }

                var latest = GetLatestTempDllFolder();
                if (string.IsNullOrEmpty(latest)) { _log.LogTrace("IsRuntimeOutdated: no latest folder -> false"); return false; }

                var (ok, hash, reason) = TryReadRuntimeGroupHash(latest);
                if (!ok)
                {
                    _log.LogWarning("IsRuntimeOutdated: invalid runtime group {Dir}: {Reason}", latest, reason);
                    return false;
                }

                var loadedHash = CurrentInfo.LoadedRuntimeAssembly.Hash;
                if (string.IsNullOrEmpty(loadedHash) || loadedHash == AttributeMetadataLoader.MissingMarker)
                {
                    _log.LogDebug("IsRuntimeOutdated: no loaded hash recorded -> true");
                    return true;
                }
                var result = !string.Equals(hash, loadedHash, StringComparison.OrdinalIgnoreCase);
                _log.LogDebug("IsRuntimeOutdated: discovered={Discovered} loaded={Loaded} result={Result}", hash, loadedHash, result);
                return result;
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

                var (ok, hash, reason) = TryReadLoaderGroupHash(loaderDir);
                if (ok)
                {
                    var loaderDll = Path.Combine(loaderDir, LoaderConstants.LoaderFileName);
                    var deployFolderMeta = File.Exists(loaderDll) ? AttributeMetadataLoader.TryGetFromFile(loaderDll, BuildConstants.DeployFolderMetadataKey) : null;
                    CurrentInfo.LoaderComponents.Path = !string.IsNullOrEmpty(deployFolderMeta) && deployFolderMeta != AttributeMetadataLoader.MissingMarker ? deployFolderMeta : AttributeMetadataLoader.MissingMarker;
                    CurrentInfo.LoaderComponents.Hash = hash;
                }
                else
                {
                    _log.LogWarning("UpdateLoaderComponents after restart: invalid group {Dir}: {Reason}", loaderDir, reason);
                    CurrentInfo.LoaderComponents.Path = AttributeMetadataLoader.MissingMarker;
                    CurrentInfo.LoaderComponents.Hash = AttributeMetadataLoader.MissingMarker;
                }
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
