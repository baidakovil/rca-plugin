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

    hostSource = sources.FirstOrDefault(s =>
        string.Equals(Path.GetFileName(s), "Rca.Integration.Revit.Tests.dll", StringComparison.OrdinalIgnoreCase))
        ?? string.Empty;

    if (string.IsNullOrEmpty(hostSource))
    {
      logger.SendMessage(TestMessageLevel.Warning, "RCA Test Adapter: No matching host source provided. Discovery skipped.");
      logger.SendMessage(TestMessageLevel.Informational, "RCA Test Adapter: Test discovery completed (0 tests)");
      return false;
    }

    var testRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RCA",
        "Test");

    var latestFolder = GetLatestFolder(testRoot);
    if (string.IsNullOrEmpty(latestFolder) || !Directory.Exists(latestFolder))
    {
      logger.SendMessage(TestMessageLevel.Informational, $"RCA Test Adapter: No test deploy folder found under {testRoot}");
      logger.SendMessage(TestMessageLevel.Informational, "RCA Test Adapter: Test discovery completed (0 tests)");
      return false;
    }

    logger.SendMessage(TestMessageLevel.Informational, $"RCA Test Adapter: Latest test folder: {latestFolder}");

    testAssemblyPath = Path.Combine(latestFolder, "Rca.Integration.Revit.Tests.dll");
    if (!File.Exists(testAssemblyPath))
    {
      logger.SendMessage(TestMessageLevel.Informational, $"RCA Test Adapter: Test assembly not found: {testAssemblyPath}");
      logger.SendMessage(TestMessageLevel.Informational, "RCA Test Adapter: Test discovery completed (0 tests)");
      hostSource = string.Empty;
      testAssemblyPath = string.Empty;
      return false;
    }

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


