using NUnit.Framework;
using Rca.Loader.Configuration;
using System;
using System.IO;
using System.Text.Json;

namespace Rca.Loader.Tests
{
    /// <summary>
    /// Tests for <see cref="SettingsService"/> class.
    /// </summary>
    [TestFixture]
    public class SettingsServiceTests
    {
        private string? _originalSettingsPath;
        private string? _testSettingsPath;

        [SetUp]
        public void Setup()
        {
            // Clear cache before each test
            SettingsService.ClearCache();

            // Store original path for potential cleanup
            _originalSettingsPath = SettingsService.SettingsFilePath;
            // Ensure test path starts as null; tests may assign a temp path during execution
            _testSettingsPath = null;
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up test settings file if created
            if (_testSettingsPath != null && File.Exists(_testSettingsPath))
            {
                try
                {
                    File.Delete(_testSettingsPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            // Clear cache after each test
            SettingsService.ClearCache();
        }

        /// <summary>
        /// Verifies that SettingsFilePath uses CommonApplicationData and correct path structure.
        /// </summary>
        [Test]
        public void SettingsFilePath_ShouldUseCommonApplicationData()
        {
            var path = SettingsService.SettingsFilePath;
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            Assert.That(path, Does.StartWith(programData));
            Assert.That(path, Does.Contain("Autodesk"));
            Assert.That(path, Does.Contain("Revit"));
            Assert.That(path, Does.Contain("Addins"));
            Assert.That(path, Does.Contain("2026"));
            Assert.That(path, Does.Contain("Revit Chat Assistant"));
            Assert.That(path, Does.EndWith("settings.json"));
        }

        /// <summary>
        /// Verifies that LoadSettings returns default settings when file doesn't exist.
        /// </summary>
        [Test]
        public void LoadSettings_WhenFileDoesNotExist_ShouldReturnDefaults()
        {
            var settings = SettingsService.LoadSettings();

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.AutoLoadRuntimeOnStartup, Is.True, "Default AutoLoadRuntimeOnStartup should be true");
        }

        /// <summary>
        /// Verifies that LoadSettings caches the result.
        /// </summary>
        [Test]
        public void LoadSettings_ShouldCacheResult()
        {
            var settings1 = SettingsService.LoadSettings();
            var settings2 = SettingsService.LoadSettings();

            Assert.That(settings1, Is.SameAs(settings2), "Should return same instance when cached");
        }

        /// <summary>
        /// Verifies that ClearCache clears the cached settings.
        /// </summary>
        [Test]
        public void ClearCache_ShouldInvalidateCache()
        {
            var settings1 = SettingsService.LoadSettings();
            SettingsService.ClearCache();
            var settings2 = SettingsService.LoadSettings();

            Assert.That(settings1, Is.Not.SameAs(settings2), "Should return different instance after cache clear");
        }

        /// <summary>
        /// Verifies that LoadSettings is thread-safe when called concurrently.
        /// </summary>
        [Test]
        public void LoadSettings_WhenCalledConcurrently_ShouldBeThreadSafe()
        {
            const int threadCount = 10;
            var settings = new Settings[threadCount];
            var threads = new System.Threading.Thread[threadCount];

            for (int i = 0; i < threadCount; i++)
            {
                int index = i;
                threads[i] = new System.Threading.Thread(() =>
                {
                    settings[index] = SettingsService.LoadSettings();
                });
            }

            foreach (var thread in threads)
            {
                thread.Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            // All threads should get the same instance due to caching
            for (int i = 1; i < threadCount; i++)
            {
                Assert.That(settings[i], Is.SameAs(settings[0]), 
                    $"Thread {i} should receive same cached instance");
            }
        }

        /// <summary>
        /// Verifies default values of Settings class.
        /// </summary>
        [Test]
        public void Settings_ShouldHaveCorrectDefaults()
        {
            var settings = new Settings();

            Assert.That(settings.AutoLoadRuntimeOnStartup, Is.True);
        }

#if DEBUG
        /// <summary>
        /// Verifies default values of DebugSettings class (DEBUG build only).
        /// </summary>
        [Test]
        public void DebugSettings_ShouldHaveCorrectDefaults()
        {
            var debugSettings = new DebugSettings();

            Assert.That(debugSettings.VerboseLogging, Is.True);
            Assert.That(debugSettings.AutoShowPanelOnLoad, Is.False);
            Assert.That(debugSettings.RestartScriptPath, 
                Is.EqualTo(@"%USERPROFILE%\rca-plugin\build\Scripts\RestartRevitGraceful.ps1"));
            Assert.That(debugSettings.RevitProjectFilePath, Is.Null);
        }

        /// <summary>
        /// Verifies that Settings includes Debug property in DEBUG builds.
        /// </summary>
        [Test]
        public void Settings_InDebugBuild_ShouldHaveDebugProperty()
        {
            var settings = new Settings();

            Assert.That(settings.Debug, Is.Not.Null);
            Assert.That(settings.Debug, Is.InstanceOf<DebugSettings>());
        }
#endif
    }
}

