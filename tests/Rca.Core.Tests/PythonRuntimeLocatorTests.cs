using FluentAssertions;
using NUnit.Framework;
using Rca.Core.Services;
using Rca.Contracts;
using System;
using System.Collections.Generic;
using System.IO;

namespace Rca.Core.Tests
{
  [TestFixture]
  public class PythonRuntimeLocatorTests
  {
    private readonly Dictionary<string, string?> originalEnvironment = new();
    private static readonly string[] EnvironmentVariablesToReset =
    {
      "RCA_PYTHONNET_PYDLL",
      "PYTHONNET_PYDLL",
      "RCA_PYTHONNET_HOME",
      "PYTHONNET_PYTHONHOME",
      "PYTHONHOME",
      "RCA_PYTHONNET_EXTRA_PYTHONPATH",
      "PYTHONPATH"
    };

    private string? tempRoot;

    [SetUp]
    public void Setup()
    {
      tempRoot = Path.Combine(Path.GetTempPath(), nameof(PythonRuntimeLocatorTests), Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(tempRoot);

      foreach (var variableName in EnvironmentVariablesToReset)
      {
        originalEnvironment[variableName] = Environment.GetEnvironmentVariable(variableName);
        Environment.SetEnvironmentVariable(variableName, null);
      }
    }

    [TearDown]
    public void TearDown()
    {
      foreach (var pair in originalEnvironment)
      {
        Environment.SetEnvironmentVariable(pair.Key, pair.Value);
      }

      if (!string.IsNullOrWhiteSpace(tempRoot) && Directory.Exists(tempRoot))
      {
        Directory.Delete(tempRoot, recursive: true);
      }
    }

    [Test, Category("Unit")]
    public void Locate_ExplicitPythonDll_TakesPrecedenceOverExplicitHome()
    {
      var explicitHome = CreateFakePythonHome(Path.Combine(tempRoot!, "explicit-home"));
      var explicitDll = Path.Combine(explicitHome, "python311.dll");
      Environment.SetEnvironmentVariable("RCA_PYTHONNET_PYDLL", explicitDll);

      Environment.SetEnvironmentVariable("RCA_PYTHONNET_HOME", CreateFakePythonHome(Path.Combine(tempRoot!, "home-override")));

      var runtime = PythonRuntimeLocator.Locate();

      runtime.IsAvailable.Should().BeTrue();
      runtime.PythonDllPath.Should().Be(explicitDll);
      runtime.SourceDescription.Should().Contain("RCA_PYTHONNET_PYDLL");
    }

    [Test, Category("Unit")]
    public void Locate_ExplicitPythonHome_BuildsPythonSearchPaths()
    {
      var pythonHome = CreateFakePythonHome(Path.Combine(tempRoot!, "python-home"));
      Environment.SetEnvironmentVariable("RCA_PYTHONNET_HOME", pythonHome);

      var runtime = PythonRuntimeLocator.Locate();

      runtime.IsAvailable.Should().BeTrue();
      runtime.PythonHome.Should().Be(pythonHome);
      runtime.SearchPaths.Should().Contain(pythonHome);
      runtime.SearchPaths.Should().Contain(Path.Combine(pythonHome, "Lib"));
      runtime.SearchPaths.Should().Contain(Path.Combine(pythonHome, "DLLs"));
      runtime.SearchPaths.Should().Contain(Path.Combine(pythonHome, "Lib", "site-packages"));
    }

    [Test, Category("Unit")]
    public void Locate_Non311Runtime_IsRejected()
    {
      var pythonHome = Path.Combine(tempRoot!, "python-home");
      Directory.CreateDirectory(pythonHome);
      File.WriteAllText(Path.Combine(pythonHome, "python312.dll"), string.Empty);
      Environment.SetEnvironmentVariable("RCA_PYTHONNET_HOME", pythonHome);

      var runtime = PythonRuntimeLocator.Locate();

      runtime.IsAvailable.Should().BeFalse();
      runtime.FailureReason.Should().Contain(PythonRuntimeStatus.SupportedVersion);
    }

    private static string CreateFakePythonHome(string homePath)
    {
      Directory.CreateDirectory(homePath);
      Directory.CreateDirectory(Path.Combine(homePath, "Lib"));
      Directory.CreateDirectory(Path.Combine(homePath, "DLLs"));
      Directory.CreateDirectory(Path.Combine(homePath, "Lib", "site-packages"));
      File.WriteAllText(Path.Combine(homePath, "python311.dll"), string.Empty);
      File.WriteAllText(Path.Combine(homePath, "python311.zip"), string.Empty);
      return homePath;
    }
  }
}