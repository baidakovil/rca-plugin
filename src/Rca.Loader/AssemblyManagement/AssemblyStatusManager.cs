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
        private const string LoaderVersionFilePattern = "LoaderVersion - *.txt";
        private const string RuntimeVersionFilePattern = "RuntimeVersion - *.txt";

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

                // Determine latest runtime folder and read runtime hash from info file
                var latest = GetLatestTempDllFolder();
                if (!string.IsNullOrEmpty(latest))
                {
                    CurrentInfo.RuntimeAssembly.Path = Path.Combine(latest, LoaderConstants.RuntimeFileName);
                    CurrentInfo.RuntimeAssembly.Hash = ReadHashFromVersionFile(latest, RuntimeVersionFilePattern) ?? string.Empty;
                }
                else
                {
                    CurrentInfo.RuntimeAssembly.Path = string.Empty;
                    CurrentInfo.RuntimeAssembly.Hash = string.Empty;
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
                return dirs.OrderBy(d => d).Last();
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
                // For new design, tempDllPath may be ignored; prefer reading latest folder
                var latest = GetLatestTempDllFolder();
                if (!string.IsNullOrEmpty(latest))
                {
                    var loaderHash = ReadHashFromVersionFile(latest, LoaderVersionFilePattern);
                    var runtimeHash = ReadHashFromVersionFile(latest, RuntimeVersionFilePattern);

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
                var latest = GetLatestTempDllFolder();
                if (string.IsNullOrEmpty(latest)) return false;
                var loaderHash = ReadHashFromVersionFile(latest, LoaderVersionFilePattern);
                return !string.IsNullOrEmpty(loaderHash) && loaderHash != CurrentInfo.LoaderComponents.Hash;
            }
            catch { return false; }
        }

        public bool IsRuntimeOutdated()
        {
            try
            {
                var latest = GetLatestTempDllFolder();
                if (string.IsNullOrEmpty(latest)) return false;
                var runtimeHash = ReadHashFromVersionFile(latest, RuntimeVersionFilePattern);
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
                var dir = Path.GetDirectoryName(runtimePath) ?? string.Empty;
                CurrentInfo.RuntimeAssembly.Hash = ReadHashFromVersionFile(dir, RuntimeVersionFilePattern) ?? string.Empty;
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
                CurrentInfo.LoaderComponents.Hash = ReadHashFromVersionFile(loaderDir, LoaderVersionFilePattern) ?? string.Empty;
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
