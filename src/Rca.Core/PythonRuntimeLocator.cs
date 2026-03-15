#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Rca.Contracts;

namespace Rca.Core.Services
{
  /// <summary>
  /// Describes a resolved CPython runtime for pythonnet.
  /// </summary>
  public sealed class PythonRuntimeConfiguration
  {
    private PythonRuntimeConfiguration(
        string? pythonDllPath,
        string? pythonHome,
        IReadOnlyList<string> searchPaths,
        string sourceDescription,
        string? failureReason)
    {
      PythonDllPath = pythonDllPath;
      PythonHome = pythonHome;
      SearchPaths = searchPaths;
      PythonPath = string.Join(Path.PathSeparator, searchPaths);
      SourceDescription = sourceDescription;
      FailureReason = failureReason;
    }

    public string? PythonDllPath { get; }

    public string? PythonHome { get; }

    public IReadOnlyList<string> SearchPaths { get; }

    public string PythonPath { get; }

    public string SourceDescription { get; }

    public string? FailureReason { get; }

    public bool IsAvailable =>
        !string.IsNullOrWhiteSpace(PythonDllPath) &&
        !string.IsNullOrWhiteSpace(PythonHome) &&
        File.Exists(PythonDllPath) &&
        Directory.Exists(PythonHome);

    public static PythonRuntimeConfiguration Available(string pythonDllPath, string pythonHome, IReadOnlyList<string> searchPaths, string sourceDescription)
    {
      return new PythonRuntimeConfiguration(pythonDllPath, pythonHome, searchPaths, sourceDescription, null);
    }

    public static PythonRuntimeConfiguration Missing(string failureReason)
    {
      return new PythonRuntimeConfiguration(null, null, Array.Empty<string>(), "unresolved", failureReason);
    }
  }

  /// <summary>
  /// Locates a CPython installation that can be used by pythonnet.
  /// </summary>
  public static class PythonRuntimeLocator
  {
    private static readonly string[] ExplicitPythonDllEnvVars = { "RCA_PYTHONNET_PYDLL", "PYTHONNET_PYDLL" };
    private static readonly string[] ExplicitPythonHomeEnvVars = { "RCA_PYTHONNET_HOME", "PYTHONNET_PYTHONHOME", "PYTHONHOME" };
    private static readonly string[] ExtraPythonPathEnvVars = { "RCA_PYTHONNET_EXTRA_PYTHONPATH", "PYTHONPATH" };
    private const string RequiredPythonDllName = "python311.dll";

    public static PythonRuntimeConfiguration Locate(string? runtimeBaseDirectory = null)
    {
      if (TryLocateFromExplicitPythonDll(out var explicitDllRuntime))
      {
        return explicitDllRuntime;
      }

      if (TryLocateFromExplicitPythonHome(out var explicitHomeRuntime))
      {
        return explicitHomeRuntime;
      }

      var candidates = EnumeratePythonHomeCandidates(runtimeBaseDirectory).ToList();
      foreach (var candidate in candidates)
      {
        if (TryCreateFromHome(candidate.Path, candidate.Source, out var runtime))
        {
          return runtime;
        }
      }

      var searchedLocations = string.Join(
          "; ",
          candidates.Select(candidate => candidate.Path).Distinct(StringComparer.OrdinalIgnoreCase).Take(6));

      return PythonRuntimeConfiguration.Missing(
          $"Python {PythonRuntimeStatus.SupportedVersion} was not found. Set RCA_PYTHONNET_PYDLL or RCA_PYTHONNET_HOME, or install Python {PythonRuntimeStatus.SupportedVersion} from python.org. " +
          $"Searched: {searchedLocations}");
    }

    private static bool TryLocateFromExplicitPythonDll(out PythonRuntimeConfiguration runtime)
    {
      foreach (var envVar in ExplicitPythonDllEnvVars)
      {
        var dllPath = Environment.GetEnvironmentVariable(envVar);
        if (string.IsNullOrWhiteSpace(dllPath))
        {
          continue;
        }

        if (TryCreateFromDll(dllPath, $"environment variable {envVar}", out runtime))
        {
          return true;
        }

        runtime = PythonRuntimeConfiguration.Missing($"RCA requires Python {PythonRuntimeStatus.SupportedVersion}. {envVar} must point to {RequiredPythonDllName}: {dllPath}");
        return true;
      }

      runtime = PythonRuntimeConfiguration.Missing("CPython DLL environment variable not set.");
      return false;
    }

    private static bool TryLocateFromExplicitPythonHome(out PythonRuntimeConfiguration runtime)
    {
      foreach (var envVar in ExplicitPythonHomeEnvVars)
      {
        var pythonHome = Environment.GetEnvironmentVariable(envVar);
        if (string.IsNullOrWhiteSpace(pythonHome))
        {
          continue;
        }

        if (TryCreateFromHome(pythonHome, $"environment variable {envVar}", out runtime))
        {
          return true;
        }

        runtime = PythonRuntimeConfiguration.Missing($"RCA requires Python {PythonRuntimeStatus.SupportedVersion}. {envVar} does not point to a valid Python {PythonRuntimeStatus.SupportedVersion} home: {pythonHome}");
        return true;
      }

      runtime = PythonRuntimeConfiguration.Missing("CPython home environment variable not set.");
      return false;
    }

    private static IEnumerable<(string Path, string Source)> EnumeratePythonHomeCandidates(string? runtimeBaseDirectory)
    {
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var candidate in EnumerateRawCandidates(runtimeBaseDirectory))
      {
        if (string.IsNullOrWhiteSpace(candidate.Path))
        {
          continue;
        }

        var normalizedPath = NormalizePath(candidate.Path);
        if (string.IsNullOrWhiteSpace(normalizedPath) || !seen.Add(normalizedPath))
        {
          continue;
        }

        yield return (normalizedPath, candidate.Source);
      }
    }

    private static IEnumerable<(string Path, string Source)> EnumerateRawCandidates(string? runtimeBaseDirectory)
    {
      _ = runtimeBaseDirectory;

      foreach (var home in EnumeratePythonHomesFromPath())
      {
        yield return (home, "PATH lookup");
      }

      foreach (var home in EnumeratePythonHomesFromRegistry())
      {
        yield return (home, "Windows registry");
      }

      foreach (var home in EnumeratePythonHomesFromStandardLocations())
      {
        yield return (home, "standard Python 3.11 install location");
      }
    }

    private static IEnumerable<string> EnumeratePythonHomesFromPath()
    {
      var pathValue = Environment.GetEnvironmentVariable("PATH");
      if (string.IsNullOrWhiteSpace(pathValue))
      {
        yield break;
      }

      foreach (var entry in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
      {
        if (!Directory.Exists(entry))
        {
          continue;
        }

        if (File.Exists(Path.Combine(entry, RequiredPythonDllName)))
        {
          yield return entry;
          continue;
        }

        if (string.Equals(Path.GetFileName(entry), "Scripts", StringComparison.OrdinalIgnoreCase))
        {
          var candidateHome = Path.GetDirectoryName(entry);
          if (!string.IsNullOrWhiteSpace(candidateHome) && File.Exists(Path.Combine(candidateHome, RequiredPythonDllName)))
          {
            yield return candidateHome;
          }
        }
      }
    }

    private static IEnumerable<string> EnumeratePythonHomesFromRegistry()
    {
      foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
      {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
          string? installPath = null;
          RegistryKey? baseKey = null;
          RegistryKey? installPathKey = null;

          try
          {
            baseKey = RegistryKey.OpenBaseKey(hive, view);
            installPathKey = baseKey.OpenSubKey(@"Software\Python\PythonCore\3.11\InstallPath");
            installPath = installPathKey?.GetValue(null) as string;
          }
          catch
          {
          }
          finally
          {
            installPathKey?.Dispose();
            baseKey?.Dispose();
          }

          if (!string.IsNullOrWhiteSpace(installPath))
          {
            yield return installPath;
          }
        }
      }
    }

    private static IEnumerable<string> EnumeratePythonHomesFromStandardLocations()
    {
      var candidates = new[]
      {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python", "Python311"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Python311"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Python311")
      };

      foreach (var candidate in candidates)
      {
        if (Directory.Exists(candidate))
        {
          yield return candidate;
        }
      }
    }

    private static bool TryCreateFromHome(string pythonHome, string source, out PythonRuntimeConfiguration runtime)
    {
      var normalizedHome = NormalizePath(pythonHome);
      if (string.IsNullOrWhiteSpace(normalizedHome) || !Directory.Exists(normalizedHome))
      {
        runtime = PythonRuntimeConfiguration.Missing($"Python home not found: {pythonHome}");
        return false;
      }

      var pythonDll = FindPythonDll(normalizedHome);
      if (string.IsNullOrWhiteSpace(pythonDll))
      {
        runtime = PythonRuntimeConfiguration.Missing($"Python {PythonRuntimeStatus.SupportedVersion} runtime not found under: {normalizedHome}. Expected {RequiredPythonDllName}.");
        return false;
      }

      var searchPaths = BuildSearchPaths(normalizedHome);
      runtime = PythonRuntimeConfiguration.Available(pythonDll, normalizedHome, searchPaths, source);
      return true;
    }

    private static bool TryCreateFromDll(string pythonDllPath, string source, out PythonRuntimeConfiguration runtime)
    {
      var normalizedDllPath = NormalizePath(pythonDllPath);
      if (string.IsNullOrWhiteSpace(normalizedDllPath) || !File.Exists(normalizedDllPath))
      {
        runtime = PythonRuntimeConfiguration.Missing($"CPython DLL not found: {pythonDllPath}");
        return false;
      }

      if (!string.Equals(Path.GetFileName(normalizedDllPath), RequiredPythonDllName, StringComparison.OrdinalIgnoreCase))
      {
        runtime = PythonRuntimeConfiguration.Missing($"RCA requires Python {PythonRuntimeStatus.SupportedVersion}. Expected {RequiredPythonDllName}, got {Path.GetFileName(normalizedDllPath)}.");
        return false;
      }

      var pythonHome = Path.GetDirectoryName(normalizedDllPath);
      if (string.IsNullOrWhiteSpace(pythonHome) || !Directory.Exists(pythonHome))
      {
        runtime = PythonRuntimeConfiguration.Missing($"CPython home not found for DLL: {normalizedDllPath}");
        return false;
      }

      var searchPaths = BuildSearchPaths(pythonHome);
      runtime = PythonRuntimeConfiguration.Available(normalizedDllPath, pythonHome, searchPaths, source);
      return true;
    }

    private static string? FindPythonDll(string pythonHome)
    {
      var pythonDll = Path.Combine(pythonHome, RequiredPythonDllName);
      return File.Exists(pythonDll) ? pythonDll : null;
    }

    private static IReadOnlyList<string> BuildSearchPaths(string pythonHome)
    {
      var searchPaths = new List<string>();
      AddPathIfPresent(searchPaths, pythonHome);

      foreach (var zipPath in Directory.EnumerateFiles(pythonHome, "python*.zip", SearchOption.TopDirectoryOnly)
          .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
      {
        AddPathIfPresent(searchPaths, zipPath);
      }

      AddPathIfPresent(searchPaths, Path.Combine(pythonHome, "Lib"));
      AddPathIfPresent(searchPaths, Path.Combine(pythonHome, "DLLs"));
      AddPathIfPresent(searchPaths, Path.Combine(pythonHome, "Lib", "site-packages"));

      foreach (var envVar in ExtraPythonPathEnvVars)
      {
        var envValue = Environment.GetEnvironmentVariable(envVar);
        if (string.IsNullOrWhiteSpace(envValue))
        {
          continue;
        }

        foreach (var pathEntry in envValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
          AddPathIfPresent(searchPaths, pathEntry);
        }
      }

      return searchPaths;
    }

    private static void AddPathIfPresent(ICollection<string> searchPaths, string? path)
    {
      if (string.IsNullOrWhiteSpace(path))
      {
        return;
      }

      var normalizedPath = NormalizePath(path);
      if (string.IsNullOrWhiteSpace(normalizedPath))
      {
        return;
      }

      if (!Directory.Exists(normalizedPath) && !File.Exists(normalizedPath))
      {
        return;
      }

      if (!searchPaths.Contains(normalizedPath))
      {
        searchPaths.Add(normalizedPath);
      }
    }

    private static string? NormalizePath(string? path)
    {
      if (string.IsNullOrWhiteSpace(path))
      {
        return null;
      }

      try
      {
        return Path.GetFullPath(path.Trim().Trim('"'));
      }
      catch
      {
        return path.Trim().Trim('"');
      }
    }
  }
}