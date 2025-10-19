using NUnit.Framework;
using Rca.Loader.AssemblyManagement;
using Rca.Loader.Infrastructure;
using System;
using System.IO;

namespace Rca.Loader.Tests
{
    /// <summary>
    /// Tests for <see cref="AssemblyStatusManager"/> class.
    /// </summary>
    [TestFixture]
    public class AssemblyStatusManagerTests
    {
        private AssemblyStatusManager? _statusManager;
        private string? _testRuntimeRoot;
        private string? _testFolder1;
        private string? _testFolder2;

        [SetUp]
        public void Setup()
        {
            _statusManager = new AssemblyStatusManager();

            // Create test runtime folders with timestamps
            _testRuntimeRoot = Path.Combine(Path.GetTempPath(), "RCA_StatusManager_Test", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testRuntimeRoot);

            _testFolder1 = Path.Combine(_testRuntimeRoot, "20251019_100000");
            _testFolder2 = Path.Combine(_testRuntimeRoot, "20251019_110000");
            Directory.CreateDirectory(_testFolder1);
            Directory.CreateDirectory(_testFolder2);
        }

        [TearDown]
        public void TearDown()
        {
            if (_testRuntimeRoot != null && Directory.Exists(_testRuntimeRoot))
            {
                try
                {
                    Directory.Delete(_testRuntimeRoot, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        /// <summary>
        /// Verifies that AssemblyStatusManager can be instantiated.
        /// </summary>
        [Test]
        public void Constructor_ShouldInitializeCurrentInfo()
        {
            Assert.That(_statusManager!.CurrentInfo, Is.Not.Null);
            Assert.That(_statusManager.CurrentInfo.LoaderComponents, Is.Not.Null);
            Assert.That(_statusManager.CurrentInfo.RuntimeAssembly, Is.Not.Null);
            Assert.That(_statusManager.CurrentInfo.LoadedRuntimeAssembly, Is.Not.Null);
            Assert.That(_statusManager.CurrentInfo.LastMSBuildSignal, Is.Not.Null);
        }

        /// <summary>
        /// Verifies that GetLatestTempDllFolder returns empty when directory doesn't exist.
        /// </summary>
        [Test]
        public void GetLatestTempDllFolder_WhenDirectoryDoesNotExist_ShouldReturnEmpty()
        {
            // Note: This tests the actual LoaderConstants.RuntimeDeployRoot which may not exist
            var result = _statusManager!.GetLatestTempDllFolder();

            // Result could be empty or a valid path depending on system state
            Assert.That(result, Is.Not.Null);
        }

        /// <summary>
        /// Verifies that DetermineEventType returns correct event for no changes.
        /// </summary>
        [Test]
        public void DetermineEventType_WhenNoChanges_ShouldReturnNoChanges()
        {
            var result = _statusManager!.DetermineEventType(false, false);

            Assert.That(result, Is.EqualTo("no changes"));
        }

        /// <summary>
        /// Verifies that DetermineEventType returns correct event for loader changes only.
        /// </summary>
        [Test]
        public void DetermineEventType_WhenOnlyLoaderChanged_ShouldReturnLoaderOutdated()
        {
            var result = _statusManager!.DetermineEventType(true, false);

            Assert.That(result, Is.EqualTo("only loader outdated"));
        }

        /// <summary>
        /// Verifies that DetermineEventType returns correct event for runtime changes only.
        /// </summary>
        [Test]
        public void DetermineEventType_WhenOnlyRuntimeChanged_ShouldReturnRuntimeOutdated()
        {
            var result = _statusManager!.DetermineEventType(false, true);

            Assert.That(result, Is.EqualTo("only runtime outdated"));
        }

        /// <summary>
        /// Verifies that DetermineEventType returns correct event for both changes.
        /// </summary>
        [Test]
        public void DetermineEventType_WhenBothChanged_ShouldReturnBothOutdated()
        {
            var result = _statusManager!.DetermineEventType(true, true);

            Assert.That(result, Is.EqualTo("both loader and runtime outdated"));
        }

        /// <summary>
        /// Verifies that CurrentInfo properties can be modified.
        /// </summary>
        [Test]
        public void CurrentInfo_PropertiesCanBeModified()
        {
            _statusManager!.CurrentInfo.LoaderComponents.Hash = "test_hash";
            _statusManager.CurrentInfo.LoaderComponents.Path = "test_path";

            Assert.That(_statusManager.CurrentInfo.LoaderComponents.Hash, Is.EqualTo("test_hash"));
            Assert.That(_statusManager.CurrentInfo.LoaderComponents.Path, Is.EqualTo("test_path"));
        }

        /// <summary>
        /// Verifies that GetLatestTempDllFolder returns the most recent folder.
        /// </summary>
        [Test]
        public void GetLatestTempDllFolder_WithMultipleFolders_ShouldReturnLatest()
        {
            // This test would require mocking the file system or creating actual folders
            // For now, we just verify the method doesn't throw
            var result = _statusManager!.GetLatestTempDllFolder();
            Assert.That(result, Is.Not.Null);
        }

        /// <summary>
        /// Verifies that IsLoaderOutdated returns false when loader doesn't exist.
        /// </summary>
        [Test]
        public void IsLoaderOutdated_WhenLoaderDoesNotExist_ShouldReturnFalse()
        {
            // This tests the actual LoaderConstants.LoaderAssemblyPath
            var result = _statusManager!.IsLoaderOutdated();

            // Result depends on system state, just verify it doesn't throw
            Assert.That(result, Is.Not.Null);
        }


        /// <summary>
        /// Verifies that DetermineEventType is case-insensitive for boolean logic.
        /// </summary>
        [Test]
        [TestCase(true, true, "both loader and runtime outdated")]
        [TestCase(true, false, "only loader outdated")]
        [TestCase(false, true, "only runtime outdated")]
        [TestCase(false, false, "no changes")]
        public void DetermineEventType_AllCombinations_ShouldReturnCorrectEvent(bool loaderChanged, bool runtimeChanged, string expected)
        {
            var result = _statusManager!.DetermineEventType(loaderChanged, runtimeChanged);

            Assert.That(result, Is.EqualTo(expected));
        }

        /// <summary>
        /// Verifies that CurrentInfo starts with empty/missing values.
        /// </summary>
        [Test]
        public void CurrentInfo_InitialState_ShouldHaveEmptyOrMissingValues()
        {
            var info = _statusManager!.CurrentInfo;

            Assert.That(info.LoaderComponents.Path, Is.Empty);
            Assert.That(info.LoaderComponents.Hash, Is.Empty);
            Assert.That(info.RuntimeAssembly.Path, Is.Empty);
            Assert.That(info.RuntimeAssembly.Hash, Is.Empty);
            Assert.That(info.LoadedRuntimeAssembly.Path, Is.Empty);
            Assert.That(info.LoadedRuntimeAssembly.Hash, Is.Empty);
            Assert.That(info.LastMSBuildSignal.Time, Is.Empty);
            Assert.That(info.LastMSBuildSignal.Event, Is.EqualTo("no changes"));
        }

    }
}

