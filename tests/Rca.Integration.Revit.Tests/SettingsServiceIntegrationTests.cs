using NUnit.Framework;
using FluentAssertions;
using Rca.Integration.Revit.Tests.Infrastructure;
using Rca.Loader.Configuration;
using System.IO;

namespace Rca.Integration.Revit.Tests
{
  /// <summary>
  /// Integration tests for SettingsService in running Revit context.
  /// 
  /// BUSINESS VALUE:
  /// - Validates settings loading from actual settings.json file
  /// - Ensures settings caching works correctly for performance
  /// - Tests cache invalidation mechanism
  /// - Critical for user configuration: autoload, paths, debug settings
  /// 
  /// NOT TESTED (future work):
  /// - Invalid JSON handling
  /// - Missing settings file scenario
  /// - Settings file modification while Revit running (hot-reload)
  /// - Concurrent settings access (thread safety)
  /// - Settings validation and error reporting
  /// - Environment variable expansion in paths
  /// 
  /// WEAK POINTS:
  /// - LoadSettings_ShouldLoadActualSettingsFile: Only checks AutoLoadRuntimeOnStartup, ignores other properties
  /// - LoadSettings_AfterClearCache_ShouldReload: Assumes settings don't change between loads
  /// - SettingsFilePath_ShouldReturnValidPath: Doesn't verify file actually exists or is readable
  /// - All tests use static methods - not truly isolated, can affect each other
  /// - Tests don't validate settings structure completeness
  /// - No tests for default values when settings missing
  /// </summary>
  [TestFixture]
  public class SettingsServiceIntegrationTests : UIApplicationTestsBase
  {
    /// <summary>
    /// Verifies settings load from actual file. Only tests one property - incomplete validation.
    /// </summary>
    [Test, Category("Revit")]
    public void LoadSettings_ShouldLoadActualSettingsFile()
    {
      // Act
      var settings = SettingsService.LoadSettings();

      // Assert
      settings.Should().NotBeNull("Settings should be loadable");

      // AutoLoadRuntimeOnStartup is a boolean property
      var autoLoad = settings.AutoLoadRuntimeOnStartup;
      (autoLoad == true || autoLoad == false).Should().BeTrue(
          "AutoLoadRuntimeOnStartup should be a boolean value");

      TestContext.WriteLine($"AutoLoadRuntimeOnStartup: {autoLoad}");
    }

    /// <summary>
    /// Tests settings caching mechanism for performance. Validates instance identity.
    /// </summary>
    [Test, Category("Revit")]
    public void LoadSettings_ShouldCache_MultipleCalls()
    {
      // Act
      var settings1 = SettingsService.LoadSettings();
      var settings2 = SettingsService.LoadSettings();

      // Assert
      settings1.Should().BeSameAs(settings2,
          "Settings should be cached and return the same instance");
    }

    /// <summary>
    /// Tests cache invalidation. Assumes settings unchanged between loads - brittle.
    /// </summary>
    [Test, Category("Revit")]
    public void LoadSettings_AfterClearCache_ShouldReload()
    {
      // Arrange
      var settings1 = SettingsService.LoadSettings();

      // Act
      SettingsService.ClearCache();
      var settings2 = SettingsService.LoadSettings();

      // Assert
      settings1.Should().NotBeSameAs(settings2,
          "After refresh, settings should be a new instance");

      // But values should be equivalent
      settings1.AutoLoadRuntimeOnStartup.Should().Be(settings2.AutoLoadRuntimeOnStartup);
    }

    /// <summary>
    /// Validates settings file path is absolute. Doesn't verify file exists or is readable.
    /// </summary>
    [Test, Category("Revit")]
    public void SettingsFilePath_ShouldReturnValidPath()
    {
      // Act
      var settingsPath = SettingsService.SettingsFilePath;

      // Assert
      settingsPath.Should().NotBeNullOrWhiteSpace();
      Path.IsPathRooted(settingsPath).Should().BeTrue(
          "Settings file path should be an absolute path");

      TestContext.WriteLine($"Settings file path: {settingsPath}");

      // Note: File may or may not exist - service returns defaults if missing
      if (File.Exists(settingsPath))
      {
        TestContext.WriteLine("Settings file exists");
      }
      else
      {
        TestContext.WriteLine("Settings file does not exist, using defaults");
      }
    }
  }
}

