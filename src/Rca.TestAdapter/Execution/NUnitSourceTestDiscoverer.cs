using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Rca.TestAdapter;

/// <summary>
/// Discovers tests from assembly sources using NUnit reflection-based discovery.
/// </summary>
internal interface ISourceTestDiscoverer
{
  /// <summary>
  /// Discovers tests from the specified assembly sources.
  /// </summary>
  /// <param name="sources">The assembly sources.</param>
  /// <param name="frameworkHandle">Framework handle for logging discovery errors.</param>
  /// <returns>The discovered test cases.</returns>
  IReadOnlyList<TestCase> DiscoverTests(IEnumerable<string> sources, IFrameworkHandle frameworkHandle);
}

/// <summary>
/// Default implementation of <see cref="ISourceTestDiscoverer"/> that uses
/// <see cref="NUnitTestDiscoverer"/> and annotates runtime assembly paths for
/// Revit integration tests.
/// </summary>
internal sealed class NUnitSourceTestDiscoverer : ISourceTestDiscoverer
{
  [SuppressMessage(
      "Microsoft.Maintainability",
      "CA1506:Avoid excessive class coupling",
      Justification = "NUnitSourceTestDiscoverer orchestrates per-source discovery and runtime path annotation; low-level NUnit reflection logic is already encapsulated in NUnitTestDiscoverer.")]
  public IReadOnlyList<TestCase> DiscoverTests(IEnumerable<string> sources, IFrameworkHandle frameworkHandle)
  {
    ArgumentNullException.ThrowIfNull(sources);
    ArgumentNullException.ThrowIfNull(frameworkHandle);

    var discoveredTests = new List<TestCase>();

    foreach (var source in sources)
    {
      try
      {
        var tests = NUnitTestDiscoverer.FindTestsInAssembly(source);

        // Annotate source-discovered tests with runtime path if this source is the runtime test assembly.
        foreach (var t in tests)
        {
          if (string.Equals(Path.GetFileName(t.Source), "Rca.Integration.Revit.Tests.dll", StringComparison.OrdinalIgnoreCase))
          {
            var testRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RCA",
                "Test");

            var latest = new DirectoryInfo(testRoot)
                .GetDirectories()
                .OrderByDescending(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault()
                ?.FullName ?? string.Empty;

            var testDll = string.IsNullOrEmpty(latest)
                ? t.Source
                : Path.Combine(latest, "Rca.Integration.Revit.Tests.dll");

            t.SetPropertyValue(AdapterProperties.RuntimeAssemblyPath, testDll);
          }
        }

        discoveredTests.AddRange(tests);
      }
      catch (Exception ex)
      {
        frameworkHandle.SendMessage(
            TestMessageLevel.Error,
            $"RCA Test Adapter: Error discovering tests in {source}: {ex.Message}");
      }
    }

    return discoveredTests;
  }
}


