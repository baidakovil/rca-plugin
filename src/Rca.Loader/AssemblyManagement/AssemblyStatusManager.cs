using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using Rca.Loader.Infrastructure;

namespace Rca.Loader.AssemblyManagement
{
    public class AssemblyStatusManager
    {
        private const string LoaderVersionFilePattern = "HashLoader - *.txt";
        private const string RuntimeVersionFilePattern = "HashRuntime - *.txt";

        public AssemblyStatusManager()
        {
        }

        public LoadedAssembliesInfo CurrentInfo { get; } = new LoadedAssembliesInfo();

        public void InitializeOnStartup()
        {
            try
            {
                // Read loader hash from current executing assembly
                var loaderAsm = Assembly.GetExecutingAssembly();
                var loaderHash = AttributeMetadataLoader.TryGetFromLoadedAssembly(loaderAsm, "SourceHash");
                var loaderDeployFolder = AttributeMetadataLoader.TryGetFromLoadedAssembly(loaderAsm, "DeployFolder");

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
                    CurrentInfo.RuntimeAssembly.Path = runtimePathFromRuntimeManager;
                    // For a runtime that is loaded into the process, prefer reflection reading
                    // to ensure we get the attributes from the in-memory assembly
                    var loadedRuntimeAsm = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => !a.IsDynamic && string.Equals(Path.GetFileName(a.Location), LoaderConstants.RuntimeFileName, StringComparison.OrdinalIgnoreCase));

                    if (loadedRuntimeAsm != null)
                    {
                        var rHash = AttributeMetadataLoader.TryGetFromLoadedAssembly(loadedRuntimeAsm, "SourceHash");
                        CurrentInfo.RuntimeAssembly.Hash = string.IsNullOrEmpty(rHash) ? AttributeMetadataLoader.MissingMarker : rHash;
                    }
                    else
                    {
                        var hash = AttributeMetadataLoader.TryGetFromFile(runtimePathFromRuntimeManager, "SourceHash");
                        CurrentInfo.RuntimeAssembly.Hash = string.IsNullOrEmpty(hash) ? AttributeMetadataLoader.MissingMarker : hash;
                    }
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
                            CurrentInfo.RuntimeAssembly.Path = runtimeDll;
                            var hash = AttributeMetadataLoader.TryGetFromFile(runtimeDll, "SourceHash");
                            CurrentInfo.RuntimeAssembly.Hash = string.IsNullOrEmpty(hash) ? AttributeMetadataLoader.MissingMarker : hash;
                        }
                        else
                        {
                            CurrentInfo.RuntimeAssembly.Path = string.Empty;
                            CurrentInfo.RuntimeAssembly.Hash = AttributeMetadataLoader.MissingMarker;
                        }
                    }
                    else
                    {
                        CurrentInfo.RuntimeAssembly.Path = string.Empty;
                        CurrentInfo.RuntimeAssembly.Hash = AttributeMetadataLoader.MissingMarker;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing assembly status: {ex.Message}");
            }
        }

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
                Debug.WriteLine($"Error reading version file in {dir}: {ex.Message}");
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
                Debug.WriteLine($"Error getting latest temp dll folder: {ex.Message}");
                return string.Empty;
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

                if (!string.IsNullOrEmpty(latest))
                {
                    // Read hashes from the actual DLL metadata (do not rely on text files)
                    string? loaderHash = null;
                    string? runtimeHash = null;

                    // Runtime hash - from runtime DLL in the folder
                    var runtimeDll = Path.Combine(latest, LoaderConstants.RuntimeFileName);
                    if (File.Exists(runtimeDll))
                    {
                        runtimeHash = AttributeMetadataLoader.TryGetFromFile(runtimeDll, "SourceHash");
                    }

                    // Loader hash - try loader DLL in the provided folder; if not present, fall back to deployed loader path
                    var loaderDllInTemp = Path.Combine(latest, LoaderConstants.LoaderFileName);
                    string? deployFolderMeta = null;
                    if (File.Exists(loaderDllInTemp))
                    {
                        loaderHash = AttributeMetadataLoader.TryGetFromFile(loaderDllInTemp, "SourceHash");
                        deployFolderMeta = AttributeMetadataLoader.TryGetFromFile(loaderDllInTemp, "DeployFolder");
                    }
                    else if (File.Exists(LoaderConstants.LoaderAssemblyPath))
                    {
                        loaderHash = AttributeMetadataLoader.TryGetFromFile(LoaderConstants.LoaderAssemblyPath, "SourceHash");
                        deployFolderMeta = AttributeMetadataLoader.TryGetFromFile(LoaderConstants.LoaderAssemblyPath, "DeployFolder");
                    }

                    // If DeployFolder metadata available prefer that for display
                    if (!string.IsNullOrEmpty(deployFolderMeta) && deployFolderMeta != AttributeMetadataLoader.MissingMarker)
                    {
                        CurrentInfo.LoaderComponents.Path = deployFolderMeta;
                    }
                    else
                    {
                        // Do not expose actual folder name; use explicit marker so developers can see metadata is missing
                        CurrentInfo.LoaderComponents.Path = AttributeMetadataLoader.MissingMarker;
                    }

                    bool loaderChanged = !string.IsNullOrEmpty(loaderHash) && loaderHash != CurrentInfo.LoaderComponents.Hash;
                    bool runtimeChanged = !string.IsNullOrEmpty(runtimeHash) && runtimeHash != CurrentInfo.RuntimeAssembly.Hash;

                    var ev = DetermineEventType(loaderChanged, runtimeChanged);
                    // record event in LastMSBuildSignal
                    CurrentInfo.LastMSBuildSignal.Time = DateTime.Now.ToString("HH:mm:ss");
                    CurrentInfo.LastMSBuildSignal.Event = ev;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing msbuild signal: {ex.Message}");
            }
        }

        public bool IsLoaderOutdated()
        {
            try
            {
                // Try to read the deployed loader's metadata
                if (File.Exists(LoaderConstants.LoaderAssemblyPath))
                {
                    var loaderHash = AttributeMetadataLoader.TryGetFromFile(LoaderConstants.LoaderAssemblyPath, "SourceHash");
                    return !string.IsNullOrEmpty(loaderHash) && loaderHash != CurrentInfo.LoaderComponents.Hash;
                }

                // If deployed loader not found, conservatively assume not outdated
                return false;
            }
            catch { return false; }
        }

        public bool IsRuntimeOutdated()
        {
            try
            {
                // If runtime isn't loaded into the process, treat as outdated so reload will occur
                var runtimeLoaded = LoaderApp.Instance?.RuntimeManager?.IsRuntimeLoaded ?? false;
                if (!runtimeLoaded) return true;

                var latest = GetLatestTempDllFolder();
                if (string.IsNullOrEmpty(latest)) return false;

                var runtimeDll = Path.Combine(latest, LoaderConstants.RuntimeFileName);
                if (!File.Exists(runtimeDll)) return false;

                var runtimeHash = AttributeMetadataLoader.TryGetFromFile(runtimeDll, "SourceHash");

                // If we don't have a recorded hash for currently loaded runtime, consider it outdated (not loaded or unknown)
                if (string.IsNullOrEmpty(CurrentInfo.RuntimeAssembly.Hash) || CurrentInfo.RuntimeAssembly.Hash == AttributeMetadataLoader.MissingMarker) return true;

                return !string.IsNullOrEmpty(runtimeHash) && runtimeHash != CurrentInfo.RuntimeAssembly.Hash;
            }
            catch { return false; }
        }

        public void UpdateHashesAfterReload(string runtimePath)
        {
            try
            {
                if (string.IsNullOrEmpty(runtimePath) || !File.Exists(runtimePath)) return;
                CurrentInfo.RuntimeAssembly.Path = runtimePath;
                var hash = AttributeMetadataLoader.TryGetFromFile(runtimePath, "SourceHash");
                CurrentInfo.RuntimeAssembly.Hash = string.IsNullOrEmpty(hash) ? AttributeMetadataLoader.MissingMarker : hash;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating hashes after reload: {ex.Message}");
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
                    var deployFolderMeta = AttributeMetadataLoader.TryGetFromFile(loaderDll, "DeployFolder");
                    CurrentInfo.LoaderComponents.Path = !string.IsNullOrEmpty(deployFolderMeta) && deployFolderMeta != AttributeMetadataLoader.MissingMarker ? deployFolderMeta : AttributeMetadataLoader.MissingMarker;

                    var hash = AttributeMetadataLoader.TryGetFromFile(loaderDll, "SourceHash");
                    CurrentInfo.LoaderComponents.Hash = string.IsNullOrEmpty(hash) ? AttributeMetadataLoader.MissingMarker : hash;
                }
                else
                {
                    CurrentInfo.LoaderComponents.Path = AttributeMetadataLoader.MissingMarker;
                    CurrentInfo.LoaderComponents.Hash = AttributeMetadataLoader.MissingMarker;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating loader hashes after restart: {ex.Message}");
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
