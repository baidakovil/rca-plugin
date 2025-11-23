using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Rca.TestAdapter;

/// <summary>
/// Locates the runtime test assembly for Revit integration tests based on the
/// latest deployment folder under the RCA test root.
/// </summary>
internal interface ITestAssemblyLocator
{
  bool TryLocateRuntimeTestAssembly(
      IEnumerable<string> sources,
      IMessageLogger logger,
      out string hostSource,
      out string testAssemblyPath);
}

/// <summary>
/// Default implementation of <see cref="ITestAssemblyLocator"/> that uses the
/// RCA test deployment convention under <c>%LOCALAPPDATA%\RCA\Test</c>.
/// </summary>
internal sealed class RevitTestAssemblyLocator : ITestAssemblyLocator
{
  /// <inheritdoc />
  public bool TryLocateRuntimeTestAssembly(
      IEnumerable<string> sources,
      IMessageLogger logger,
      out string hostSource,
      out string testAssemblyPath)
  {
    if (sources is null)
      throw new ArgumentNullException(nameof(sources));
    if (logger is null)
      throw new ArgumentNullException(nameof(logger));

    hostSource = string.Empty;
    testAssemblyPath = string.Empty;

    if (!TryResolveHostSource(sources, logger, out hostSource))
    {
      return false;
    }

    var testRoot = GetTestRoot();
    if (!TryResolveTestAssemblyPath(testRoot, logger, out testAssemblyPath))
    {
      hostSource = string.Empty;
      return false;
    }

    return true;
  }

  /// <summary>
  /// Determines the host source DLL from the list of VSTest sources.
  /// </summary>
  /// <param name="sources">Sources provided by VSTest.</param>
  /// <param name="logger">Logger used for diagnostic messages.</param>
  /// <param name="hostSource">The resolved host source path, if any.</param>
  /// <returns>
  /// <see langword="true"/> if a matching host source was found; otherwise <see langword="false"/>.
  /// </returns>
  private static bool TryResolveHostSource(
      IEnumerable<string> sources,
      IMessageLogger logger,
      out string hostSource)
  {
    hostSource = sources
        .FirstOrDefault(s =>
            string.Equals(Path.GetFileName(s), "Rca.Integration.Revit.Tests.dll", StringComparison.OrdinalIgnoreCase))
        ?? string.Empty;

    if (!string.IsNullOrEmpty(hostSource))
    {
      return true;
    }

    logger.SendMessage(TestMessageLevel.Warning, "RCA Test Adapter: No matching host source provided. Discovery skipped.");
    logger.SendMessage(TestMessageLevel.Informational, "RCA Test Adapter: Test discovery completed (0 tests)");
    return false;
  }

  /// <summary>
  /// Builds the RCA test root path under the local application data folder.
  /// </summary>
  private static string GetTestRoot()
  {
    return Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RCA",
        "Test");
  }

  /// <summary>
  /// Resolves the concrete runtime test assembly path from the RCA test root,
  /// using the latest deployment folder.
  /// </summary>
  /// <param name="testRoot">The RCA test root directory.</param>
  /// <param name="logger">Logger used for diagnostic messages.</param>
  /// <param name="testAssemblyPath">The resolved runtime test assembly path, if any.</param>
  /// <returns>
  /// <see langword="true"/> if the runtime assembly path was resolved successfully;
  /// otherwise <see langword="false"/>.
  /// </returns>
  private static bool TryResolveTestAssemblyPath(
      string testRoot,
      IMessageLogger logger,
      out string testAssemblyPath)
  {
    testAssemblyPath = string.Empty;

    var latestFolder = GetLatestFolder(testRoot);
    if (string.IsNullOrEmpty(latestFolder))
    {
      logger.SendMessage(TestMessageLevel.Informational, $"RCA Test Adapter: No test deploy folder found under {testRoot}");
      logger.SendMessage(TestMessageLevel.Informational, "RCA Test Adapter: Test discovery completed (0 tests)");
      return false;
    }

    logger.SendMessage(TestMessageLevel.Informational, $"RCA Test Adapter: Latest test folder: {latestFolder}");

    var candidate = Path.Combine(latestFolder, "Rca.Integration.Revit.Tests.dll");
    if (!File.Exists(candidate))
    {
      logger.SendMessage(TestMessageLevel.Informational, $"RCA Test Adapter: Test assembly not found: {candidate}");
      logger.SendMessage(TestMessageLevel.Informational, "RCA Test Adapter: Test discovery completed (0 tests)");
      return false;
    }

    testAssemblyPath = candidate;
    return true;
  }

  private static string GetLatestFolder(string root)
  {
    try
    {
      if (!Directory.Exists(root))
      {
        return string.Empty;
      }

      var di = new DirectoryInfo(root);
      var latest = di.GetDirectories()
          .OrderByDescending(d => d.Name, StringComparer.OrdinalIgnoreCase)
          .FirstOrDefault();

      return latest?.FullName ?? string.Empty;
    }
    catch
    {
      return string.Empty;
    }
  }
}


