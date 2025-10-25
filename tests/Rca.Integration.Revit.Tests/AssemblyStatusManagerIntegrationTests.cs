using NUnit.Framework;
using FluentAssertions;
using Rca.Integration.Revit.Tests.Infrastructure;
using Rca.Loader;
using Rca.Loader.AssemblyManagement;
using System.IO;
using Rca.Generated;

namespace Rca.Integration.Revit.Tests
{
    /// <summary>
    /// Integration tests for AssemblyStatusManager in running Revit context.
    /// 
    /// BUSINESS VALUE:
    /// - Validates hot-reload infrastructure: detects when loader/runtime DLLs are outdated
    /// - Ensures assembly hash tracking works correctly for change detection
    /// - Verifies MSBuild signal processing updates status correctly
    /// - Tests critical path for developer experience: fast iteration without Revit restart
    /// - Confirms timestamp folder discovery for latest built assemblies
    /// 
    /// NOT TESTED (future work):
    /// - Concurrent MSBuild signals (race conditions)
    /// - Corrupted/invalid assembly files
    /// - Hash mismatch detection between loader components
    /// - Behavior when temp folder is deleted while Revit running
    /// - Status display updates (UI integration)
    /// - Edge cases: empty timestamp folders, missing hash metadata
    /// 
    /// WEAK POINTS:
    /// - IsLoaderOutdated/IsRuntimeOutdated: Weak assertions - only check boolean without context validation
    /// - CurrentInfo_RuntimeAssembly_ShouldBeTracked: Assumes discovery happened, but doesn't force it
    /// - Timestamp folder regex is magic pattern, not derived from actual format constants
    /// </summary>
    [TestFixture]
    public class AssemblyStatusManagerIntegrationTests : UIApplicationTestsBase
    {
        /// <summary>
        /// Verifies discovery of latest timestamped build output folder.
        /// Critical for hot-reload: must find newest DLLs.
        /// </summary>
        [Test, Category("Revit")]
        public void GetLatestTempDllFolder_ShouldReturnValidPath()
        {
            // Arrange
            var statusManager = LoaderApp.Instance?.AssemblyStatusManager;
            statusManager.Should().NotBeNull();

            // Act
            var latestFolder = statusManager!.GetLatestTempDllFolder();

            // Fail the test explicitly if no latest folder is discovered. Integration
            // tests should surface missing environment setup as failures so CI is aware.
            latestFolder.Should().NotBeNullOrWhiteSpace("A latest temp DLL folder must be present for this integration test");
            Directory.Exists(latestFolder).Should().BeTrue("Latest temp DLL folder should exist");

            // Verify the folder name matches timestamp pattern (YYYYMMDD_HHMMSS)
            var folderName = Path.GetFileName(latestFolder);
            folderName.Should().MatchRegex(@"^\d{8}_\d{6}$", "Folder name should match timestamp pattern");
        }

        /// <summary>
        /// Validates loader component hash tracking. Hash is used to detect code changes.
        /// </summary>
        [Test, Category("Revit")]
        public void CurrentInfo_LoaderComponents_ShouldHaveHashOrMissingMarker()
        {
            // Arrange
            var statusManager = LoaderApp.Instance?.AssemblyStatusManager;
            statusManager.Should().NotBeNull();

            // Act
            var loaderHash = statusManager!.CurrentInfo.LoaderComponents.Hash;

            // Assert
            loaderHash.Should().NotBeNullOrWhiteSpace(
                "Loader components hash should be set after initialization");
            
            // Hash should be either a valid hash string (configured length+) or the missing marker
            (loaderHash.Length == RcaBuildMetadata.SourceHashLength || loaderHash == "[MISSING]").Should().BeTrue(
                $"Hash should be either a valid hash ({RcaBuildMetadata.SourceHashLength} chars) or missing marker");
        }

        /// <summary>
        /// Checks loader component path is tracked for change detection.
        /// </summary>
        [Test, Category("Revit")]
        public void CurrentInfo_LoaderComponents_ShouldHavePathOrMissingMarker()
        {
            // Arrange
            var statusManager = LoaderApp.Instance?.AssemblyStatusManager;
            statusManager.Should().NotBeNull();

            // Act
            var loaderPath = statusManager!.CurrentInfo.LoaderComponents.Path;

            // Assert
            loaderPath.Should().NotBeNullOrWhiteSpace(
                "Loader components path should be set after initialization");
        }

        /// <summary>
        /// Tests event type determination logic. This is unit test, not integration test
        /// Here because it's needed RevitAPI to be loaded.
        /// </summary>
        [Test, Category("Revit")]
        public void DetermineEventType_ShouldReturnCorrectEventTypes()
        {
            // Arrange
            var statusManager = LoaderApp.Instance?.AssemblyStatusManager;
            statusManager.Should().NotBeNull();

            // Act & Assert
            statusManager!.DetermineEventType(false, false)
                .Should().Be(AssemblyStatusManager.EventNoChanges);

            statusManager.DetermineEventType(true, false)
                .Should().Be(AssemblyStatusManager.EventOnlyLoaderOutdated);

            statusManager.DetermineEventType(false, true)
                .Should().Be(AssemblyStatusManager.EventOnlyRuntimeOutdated);

            statusManager.DetermineEventType(true, true)
                .Should().Be(AssemblyStatusManager.EventBothLoaderAndRuntimeOutdated);
        }

        /// <summary>
        /// Verifies loader outdated detection doesn't crash. Weak - doesn't validate actual logic.
        /// </summary>
        [Test, Category("Revit")]
        public void IsLoaderOutdated_ShouldReturnBooleanWithoutError()
        {
            // Arrange
            var statusManager = LoaderApp.Instance?.AssemblyStatusManager;
            statusManager.Should().NotBeNull();

            // Act
            var isOutdated = statusManager!.IsLoaderOutdated();

            // Assert
            // Just verify it doesn't throw - the actual value depends on whether there's a newer build
            (isOutdated == true || isOutdated == false).Should().BeTrue();
        }

        /// <summary>
        /// Verifies runtime outdated detection doesn't crash. Weak - doesn't validate actual logic.
        /// </summary>
        [Test, Category("Revit")]
        public void IsRuntimeOutdated_ShouldReturnBooleanWithoutError()
        {
            // Arrange
            var statusManager = LoaderApp.Instance?.AssemblyStatusManager;
            statusManager.Should().NotBeNull();

            // Act
            var isOutdated = statusManager!.IsRuntimeOutdated();

            // Assert
            // Just verify it doesn't throw - the actual value depends on runtime load state
            (isOutdated == true || isOutdated == false).Should().BeTrue();
        }

        /// <summary>
        /// Validates MSBuild signal tracking state. Uses magic strings for event types.
        /// </summary>
        [Test, Category("Revit")]
        public void CurrentInfo_LastMSBuildSignal_ShouldHaveDefaultOrValidTime()
        {
            // Arrange
            var statusManager = LoaderApp.Instance?.AssemblyStatusManager;
            statusManager.Should().NotBeNull();

            // Act
            var signalTime = statusManager!.CurrentInfo.LastMSBuildSignal.Time;
            var signalEvent = statusManager.CurrentInfo.LastMSBuildSignal.Event;

            // Assert
            signalTime.Should().NotBeNull("Signal time should be initialized");
            signalEvent.Should().NotBeNullOrWhiteSpace("Signal event should be initialized");
            
            // Event should be a known type
            signalEvent.Should().BeOneOf(
                "no changes",
                "only loader outdated",
                "only runtime outdated",
                "both loader and runtime outdated");
        }

        /// <summary>
        /// Tests MSBuild signal processing updates timestamp. Brittle: uses Thread.Sleep for timing.
        /// </summary>
        [Test, Category("Revit"), Category("DebugOnly")]
        public void ProcessMsBuildSignal_WithValidPath_ShouldUpdateSignalInfo()
        {
            // Arrange
            var statusManager = LoaderApp.Instance?.AssemblyStatusManager;
            statusManager.Should().NotBeNull();
            
            var latestFolder = statusManager!.GetLatestTempDllFolder();
            // Fail the test explicitly if no temp DLL folder is available. Integration tests
            // should surface missing environment conditions as failures so CI is aware.
            latestFolder.Should().NotBeNullOrWhiteSpace("A temp DLL folder must exist for this integration test to run");

            var oldTimestamp = statusManager.CurrentInfo.LastMSBuildSignal.Timestamp;

            // Act
            statusManager.ProcessMsBuildSignal(latestFolder);
            var newTimestamp = statusManager.CurrentInfo.LastMSBuildSignal.Timestamp;

            // Assert
            // Timestamp is stored in ISO format and should be updated to a new value
            newTimestamp.Should().NotBeNullOrWhiteSpace("Timestamp should be set after processing MSBuild signal");
            if (!string.IsNullOrEmpty(oldTimestamp))
            {
                newTimestamp.Should().NotBe(oldTimestamp, "Timestamp should be updated after processing MSBuild signal");
            }
        }

        /// <summary>
        /// Verifies runtime assembly discovery and tracking. May return empty if runtime not discovered yet.
        /// </summary>
        [Test, Category("Revit")]
        public void CurrentInfo_RuntimeAssembly_ShouldBeTracked()
        {
            // Arrange
            var statusManager = LoaderApp.Instance?.AssemblyStatusManager;
            statusManager.Should().NotBeNull();

            // Act
            var runtimePath = statusManager!.CurrentInfo.RuntimeAssembly.Path;
            var runtimeHash = statusManager.CurrentInfo.RuntimeAssembly.Hash;

            // Assert
            runtimePath.Should().NotBeNull("Runtime assembly path should be tracked");
            runtimeHash.Should().NotBeNull("Runtime assembly hash should be tracked");
            
            // If runtime was discovered, path and hash should be meaningful
            if (!string.IsNullOrEmpty(runtimePath))
            {
                runtimeHash.Should().NotBe("[MISSING]", 
                    "If runtime path is set, hash should also be valid");
            }
        }

        /// <summary>
        /// Tests tracking of currently loaded runtime assembly (may differ from discovered version).
        /// </summary>
        [Test, Category("Revit")]
        public void CurrentInfo_LoadedRuntimeAssembly_ShouldBeTrackedSeparately()
        {
            // Arrange
            var statusManager = LoaderApp.Instance?.AssemblyStatusManager;
            statusManager.Should().NotBeNull();

            // Act
            var loadedPath = statusManager!.CurrentInfo.LoadedRuntimeAssembly.Path;
            var loadedHash = statusManager.CurrentInfo.LoadedRuntimeAssembly.Hash;

            // Assert
            loadedPath.Should().NotBeNull("Loaded runtime path should be tracked");
            loadedHash.Should().NotBeNull("Loaded runtime hash should be tracked");
            
            // Loaded and discovered runtime info may differ if runtime hasn't been loaded yet
            // or if there's a newer version on disk
        }
    }
}

