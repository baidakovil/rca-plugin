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
        private const string LoaderVersionFilePattern = "_LoaderVersion - *.txt";
        private const string RuntimeVersionFilePattern = "_RuntimeVersion - *.txt";

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
                var loaderHash = GetAssemblyMetadata(loaderAsm, "SourceHash");
                var loaderDeployFolder = GetAssemblyMetadata(loaderAsm, "DeployFolder");

                CurrentInfo.LoaderComponents.Path = loaderAsm.Location != null ? Path.GetDirectoryName(loaderAsm.Location) ?? string.Empty : string.Empty;
                CurrentInfo.LoaderComponents.Hash = loaderHash ?? string.Empty;

                // Determine runtime - prefer actually loaded runtime if available
                string? runtimePathFromRuntimeManager = LoaderApp.Instance?.RuntimeManager?.CurrentRuntimePath;
                if (!string.IsNullOrEmpty(runtimePathFromRuntimeManager) && File.Exists(runtimePathFromRuntimeManager))
                {
                    CurrentInfo.RuntimeAssembly.Path = runtimePathFromRuntimeManager;
                    var hash = AssemblyMetadataReader.TryGetAssemblyMetadata(runtimePathFromRuntimeManager, "SourceHash");
                    CurrentInfo.RuntimeAssembly.Hash = hash ?? string.Empty;
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
                            var hash = AssemblyMetadataReader.TryGetAssemblyMetadata(runtimeDll, "SourceHash");
                            CurrentInfo.RuntimeAssembly.Hash = hash ?? string.Empty;
                        }
                        else
                        {
                            CurrentInfo.RuntimeAssembly.Path = string.Empty;
                            CurrentInfo.RuntimeAssembly.Hash = string.Empty;
                        }
                    }
                    else
                    {
                        CurrentInfo.RuntimeAssembly.Path = string.Empty;
                        CurrentInfo.RuntimeAssembly.Hash = string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing assembly status: {ex.Message}");
            }
        }

        private string? GetAssemblyMetadata(Assembly asm, string key)
        {
            try
            {
                var attrs = asm.GetCustomAttributes<AssemblyMetadataAttribute>();
                var match = attrs.FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase));
                return match?.Value;
            }
            catch
            {
                return null;
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
                        runtimeHash = AssemblyMetadataReader.TryGetAssemblyMetadata(runtimeDll, "SourceHash");
                    }

                    // Loader hash - try loader DLL in the provided folder; if not present, fall back to deployed loader path
                    var loaderDllInTemp = Path.Combine(latest, LoaderConstants.LoaderFileName);
                    if (File.Exists(loaderDllInTemp))
                    {
                        loaderHash = AssemblyMetadataReader.TryGetAssemblyMetadata(loaderDllInTemp, "SourceHash");
                    }
                    else if (File.Exists(LoaderConstants.LoaderAssemblyPath))
                    {
                        loaderHash = AssemblyMetadataReader.TryGetAssemblyMetadata(LoaderConstants.LoaderAssemblyPath, "SourceHash");
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
                    var loaderHash = AssemblyMetadataReader.TryGetAssemblyMetadata(LoaderConstants.LoaderAssemblyPath, "SourceHash");
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
                var latest = GetLatestTempDllFolder();
                if (string.IsNullOrEmpty(latest)) return false;

                var runtimeDll = Path.Combine(latest, LoaderConstants.RuntimeFileName);
                if (!File.Exists(runtimeDll)) return false;

                var runtimeHash = AssemblyMetadataReader.TryGetAssemblyMetadata(runtimeDll, "SourceHash");

                // If we don't have a recorded hash for currently loaded runtime, consider it outdated (not loaded or unknown)
                if (string.IsNullOrEmpty(CurrentInfo.RuntimeAssembly.Hash)) return true;

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
                var hash = AssemblyMetadataReader.TryGetAssemblyMetadata(runtimePath, "SourceHash");
                CurrentInfo.RuntimeAssembly.Hash = hash ?? string.Empty;
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
                CurrentInfo.LoaderComponents.Path = loaderDir;

                var loaderDll = Path.Combine(loaderDir, LoaderConstants.LoaderFileName);
                if (File.Exists(loaderDll))
                {
                    var hash = AssemblyMetadataReader.TryGetAssemblyMetadata(loaderDll, "SourceHash");
                    CurrentInfo.LoaderComponents.Hash = hash ?? string.Empty;
                }
                else
                {
                    CurrentInfo.LoaderComponents.Hash = string.Empty;
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
