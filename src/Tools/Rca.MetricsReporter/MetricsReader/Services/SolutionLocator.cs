namespace Rca.Tools.MetricsReporter.MetricsReader.Services;

using System;
using System.IO;
using System.Linq;

/// <summary>
/// Locates the solution file associated with a metrics report path.
/// </summary>
internal static class SolutionLocator
{
  public static string FindSolutionPath(string reportPath)
  {
    var directory = GetStartingDirectory(reportPath);
    while (directory is not null)
    {
      var solution = TryResolveSolution(directory.FullName);
      if (solution is not null)
      {
        return solution;
      }

      directory = directory.Parent;
    }

    throw new InvalidOperationException($"Failed to discover a solution file while walking up from '{reportPath}'.");
  }

  private static DirectoryInfo? GetStartingDirectory(string path)
  {
    if (File.Exists(path))
    {
      return new DirectoryInfo(Path.GetDirectoryName(path)!);
    }

    return new DirectoryInfo(Path.GetFullPath(path));
  }

  private static string? TryResolveSolution(string directory)
  {
    var solutions = Directory.GetFiles(directory, "*.sln");
    if (solutions.Length == 0)
    {
      return null;
    }

    var preferred = solutions.FirstOrDefault(s => string.Equals(Path.GetFileName(s), "rca-plugin.sln", StringComparison.OrdinalIgnoreCase));
    return preferred ?? solutions[0];
  }
}

