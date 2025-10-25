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
    /// IMPORTANT: Metadata is ALWAYS read from files on disk, never from already loaded assemblies
    /// </summary>
    public class AssemblyStatusManager
    {
        // Public constants for MSBuild signal event types. Tests should reference
        // these constants instead of relying on magic strings.
        public const string EventNoChanges = "no changes";
        public const string EventOnlyLoaderOutdated = "only loader outdated";
        public const string EventOnlyRuntimeOutdated = "only runtime outdated";
        public const string EventBothLoaderAndRuntimeOutdated = "both loader and runtime outdated";

        private readonly ILogger _log = LoaderLog.GetLogger<AssemblyStatusManager>();

        public AssemblyStatusManager() { }

        public LoadedAssembliesInfo CurrentInfo { get; } = new LoadedAssembliesInfo();

        /// <summary>
        /// Initializes status at Loader startup. Reads metadata only from disk
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
                // Prefer sticky timestamp file produced by MSBuild under Addins root
                var stampFile = Path.Combine(LoaderConstants.RevitAddinDir, "Timestamp.txt");
                if (File.Exists(stampFile))
                {
                    var stamp = (File.ReadAllText(stampFile) ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(stamp))
                    {
                        var folder = Path.Combine(LoaderConstants.RevitAddinDir, stamp);
                        if (Directory.Exists(folder)) return folder;
                    }
                }

                // Fallback: pick the most recent timestamp-like directory under Addins root
                if (!Directory.Exists(LoaderConstants.RuntimeDeployRoot)) return string.Empty;
                var dirs = Directory.GetDirectories(LoaderConstants.RuntimeDeployRoot)
                    .Where(d => Path.GetFileName(d)!.Length == 15 && Path.GetFileName(d)!.Contains("_"))
                    .OrderByDescending(d => Path.GetFileName(d))
                    .ToArray();
                if (dirs.Length == 0) return string.Empty;
                return dirs.First();
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

                // Also refresh loader from latest deploy if missing
                if (CurrentInfo.LoaderComponents.Hash == AttributeMetadataLoader.MissingMarker)
                {
                    var (lok, lhash, _) = TryReadLoaderGroupHash(folder);
                    if (lok)
                    {
                        CurrentInfo.LoaderComponents.Hash = lhash;
                        var stamp = Path.GetFileName(folder);
                        if (string.IsNullOrEmpty(CurrentInfo.LoaderComponents.Path) || CurrentInfo.LoaderComponents.Path == AttributeMetadataLoader.MissingMarker)
                            CurrentInfo.LoaderComponents.Path = stamp ?? AttributeMetadataLoader.MissingMarker;
                        _log.LogDebug("Loader info refreshed from runtime folder after reload stamp={Stamp} hash={Hash}", stamp, lhash);
                    }
                }

                _log.LogInformation("Runtime hash updated after reload oldLoaded={OldHash} newLoaded={NewHash}", oldLoadedHash, finalHash);
                try { LoaderApp.Instance?.UpdateStatusDisplay(); } catch { }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error updating hashes after reload");
            }
        }

        /// <summary>
        /// Processes signal from MSBuild that a new build is available. Resolves latest folder and compares both groups.
        /// </summary>
        public void ProcessMsBuildSignal(string tempDllPath)
        {
            try
            {
                var latest = !string.IsNullOrEmpty(tempDllPath) && Directory.Exists(tempDllPath) ? tempDllPath : GetLatestTempDllFolder();
                _log.LogDebug("ProcessMsBuildSignal tempDllPath={Temp} resolved={Resolved}", tempDllPath, latest);
                if (string.IsNullOrEmpty(latest)) return;

                // Runtime discovered from latest
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

                // Loader installed (addin dir)
                var oldLoaderHashSnapshot = CurrentInfo.LoaderComponents.Hash;
                if (File.Exists(LoaderConstants.LoaderAssemblyPath))
                {
                    var lHashInstalled = AttributeMetadataLoader.TryGetFromFile(LoaderConstants.LoaderAssemblyPath, BuildConstants.SourceHashMetadataKey);
                    var lDeployInstalled = AttributeMetadataLoader.TryGetFromFile(LoaderConstants.LoaderAssemblyPath, BuildConstants.DeployFolderMetadataKey);
                    if (!string.IsNullOrEmpty(lHashInstalled) && lHashInstalled != AttributeMetadataLoader.MissingMarker)
                    {
                        CurrentInfo.LoaderComponents.Hash = lHashInstalled;
                        if (!string.IsNullOrEmpty(lDeployInstalled) && lDeployInstalled != AttributeMetadataLoader.MissingMarker)
                            CurrentInfo.LoaderComponents.Path = lDeployInstalled;
                        _log.LogDebug("Loader installed info: hash={Hash} deploy={Deploy}", lHashInstalled, lDeployInstalled);
                    }
                }

                // Loader latest (from latest folder)
                var (lokLatest, lHashLatest, lReasonLatest) = TryReadLoaderGroupHash(latest);
                if (lokLatest)
                {
                    _log.LogDebug("Discovered loader build in latest folder hash={Hash} dir={Dir}", lHashLatest, latest);
                }
                else
                {
                    _log.LogDebug("No valid loader build discovered in latest folder: {Reason}", lReasonLatest);
                }

                // Determine changes: loaderChanged compares latest vs installed; runtimeChanged compares discovered vs loaded
                bool loaderChanged = lokLatest && !string.Equals(lHashLatest, CurrentInfo.LoaderComponents.Hash, StringComparison.OrdinalIgnoreCase);
                bool runtimeChanged = !string.IsNullOrEmpty(CurrentInfo.LoadedRuntimeAssembly.Hash) &&
                                      !string.Equals(CurrentInfo.RuntimeAssembly.Hash, CurrentInfo.LoadedRuntimeAssembly.Hash, StringComparison.OrdinalIgnoreCase);
                var ev = DetermineEventType(loaderChanged, runtimeChanged);

                var oldSignal = $"{CurrentInfo.LastMSBuildSignal.Time} - {CurrentInfo.LastMSBuildSignal.Event}";
                // Preserve the short display `Time` for UI while also storing a full ISO timestamp
                CurrentInfo.LastMSBuildSignal.Time = DateTime.Now.ToString("HH:mm:ss");
                CurrentInfo.LastMSBuildSignal.Timestamp = DateTime.Now.ToString("o");
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
                // Compare installed loader (addin dir) vs latest discovered loader in runtime deploy root
                if (!File.Exists(LoaderConstants.LoaderAssemblyPath)) return false;
                var installedHash = AttributeMetadataLoader.TryGetFromFile(LoaderConstants.LoaderAssemblyPath, BuildConstants.SourceHashMetadataKey);
                var latestDir = GetLatestTempDllFolder();
                if (string.IsNullOrEmpty(latestDir)) return false;
                var (lok, latestHash, _) = TryReadLoaderGroupHash(latestDir);
                if (!lok || string.IsNullOrEmpty(installedHash) || installedHash == AttributeMetadataLoader.MissingMarker) return false;
                var result = !string.Equals(installedHash, latestHash, StringComparison.OrdinalIgnoreCase);
                _log.LogDebug("IsLoaderOutdated installed={Installed} latest={Latest} result={Result}", installedHash, latestHash, result);
                return result;
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
            if (loaderComponentsChanged && runtimeChanged) return EventBothLoaderAndRuntimeOutdated;
            if (loaderComponentsChanged) return EventOnlyLoaderOutdated;
            if (runtimeChanged) return EventOnlyRuntimeOutdated;
            return EventNoChanges;
        }
    }
}
