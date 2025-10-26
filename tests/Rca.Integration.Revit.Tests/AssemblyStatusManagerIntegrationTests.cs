using NUnit.Framework;
using FluentAssertions;
using Rca.Integration.Revit.Tests.Infrastructure;
using Rca.Loader;
using Rca.Loader.AssemblyManagement;
using Rca.Loader.Infrastructure;
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
    /// - CurrentInfo_RuntimeAssembly_ShouldBeTracked: Assumes discovery happened, but doesn't force it
    /// </summary>
    [TestFixture]
    public class AssemblyStatusManagerIntegrationTests : UIApplicationTestsBase
    {
        private string? _testLatestFolder;
        private readonly System.Reflection.Assembly _testAssembly = System.Reflection.Assembly.GetExecutingAssembly();

        [SetUp]
        public void IntegrationSetUp()
        {
            _testLatestFolder = null;
        }

        [TearDown]
        public void IntegrationTearDown()
        {
            try { if (!string.IsNullOrEmpty(_testLatestFolder) && Directory.Exists(_testLatestFolder)) Directory.Delete(_testLatestFolder, recursive: true); } catch { }
            _testLatestFolder = null;
        }


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

            // Verify the folder name matches timestamp pattern (YYYYMMDD_HHMMSS) using DateTime.TryParseExact
            var folderName = Path.GetFileName(latestFolder);

            // The expected format is "yyyyMMdd_HHmmss"
            var parsed = DateTime.TryParseExact(
                folderName,
                "yyyyMMdd_HHmmss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var _);

            parsed.Should().BeTrue("Folder name should match the timestamp pattern 'yyyyMMdd_HHmmss'");
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
        /// Verifies loader outdated detection doesn't crash
        /// </summary>
        [Test, Category("Revit")]
        public void IsLoaderOutdated_WhenLatestMatchesInstalled_ShouldReturnFalse()
        {
            // Arrange
            var statusManager = LoaderApp.Instance?.AssemblyStatusManager;
            statusManager.Should().NotBeNull();

            // Validate installed loader assembly and its SourceHash metadata — fail the test
            // if these environment prerequisites are not met.
            var installedPath = LoaderConstants.LoaderAssemblyPath;
            File.Exists(installedPath).Should().BeTrue($"Installed loader assembly must exist at {installedPath}");
            var installedHash = AttributeMetadataLoader.TryGetFromFile(installedPath, BuildConstants.SourceHashMetadataKey);
            installedHash.Should().NotBe(AttributeMetadataLoader.MissingMarker, "Installed loader assembly must contain SourceHash metadata for this integration test");

            // Setup: create a latest timestamp folder under RuntimeDeployRoot and copy current
            // loader assemblies into it so the latest group hash matches installed.
            var stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var latestFolder = Path.Combine(LoaderConstants.RuntimeDeployRoot, stamp);
            _testLatestFolder = latestFolder;
            try
            {
                Directory.CreateDirectory(latestFolder);
                foreach (var dll in LoaderConstants.LoaderAssemblies)
                {
                    var src = Path.Combine(LoaderConstants.RcaAddinDir, dll);
                    File.Exists(src).Should().BeTrue($"Required loader assembly {dll} must exist in addin dir ({LoaderConstants.RcaAddinDir}) to run this integration test");
                    var dest = Path.Combine(latestFolder, dll);
                    File.Copy(src, dest, overwrite: true);
                }

                // Act
                var isOutdated = statusManager!.IsLoaderOutdated();

                // Assert
                // When latest matches installed, loader should not be considered outdated
                isOutdated.Should().BeFalse("When the latest loader files match the installed ones, IsLoaderOutdated should return false");
            }
            finally
            {
                try { if (Directory.Exists(latestFolder)) Directory.Delete(latestFolder, recursive: true); } catch { }
            }
        }

        /// <summary>
        /// Verifies runtime outdated detection doesn't crash when latest matches installed.
        /// </summary>
        [Test, Category("Revit")]
        public void IsRuntimeOutdated_WhenLatestMatchesInstalled_ShouldReturnFalse()
        {
            // Arrange
            var statusManager = LoaderApp.Instance?.AssemblyStatusManager;
            statusManager.Should().NotBeNull();

            // Validate installed runtime assembly and its SourceHash metadata — fail the test
            // if these environment prerequisites are not met.
            var installedPath = Path.Combine(LoaderConstants.RcaAddinDir, LoaderConstants.RuntimeFileName);
            File.Exists(installedPath).Should().BeTrue($"Installed runtime assembly must exist at {installedPath}");
            var installedHash = AttributeMetadataLoader.TryGetFromFile(installedPath, BuildConstants.SourceHashMetadataKey);
            installedHash.Should().NotBe(AttributeMetadataLoader.MissingMarker, "Installed runtime assembly must contain SourceHash metadata for this integration test");

            // Setup: create a latest timestamp folder under RuntimeDeployRoot and copy current
            // runtime assemblies into it so the latest group hash matches installed.
            var stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var latestFolder = Path.Combine(LoaderConstants.RuntimeDeployRoot, stamp);
            _testLatestFolder = latestFolder;
            try
            {
                Directory.CreateDirectory(latestFolder);
                foreach (var dll in LoaderConstants.RuntimeAssemblies)
                {
                    var src = Path.Combine(LoaderConstants.RcaAddinDir, dll);
                    File.Exists(src).Should().BeTrue($"Required runtime assembly {dll} must exist in addin dir ({LoaderConstants.RcaAddinDir}) to run this integration test");
                    var dest = Path.Combine(latestFolder, dll);
                    File.Copy(src, dest, overwrite: true);
                }

                // Act
                var isOutdated = statusManager!.IsRuntimeOutdated();

                // Assert
                // When latest matches installed, runtime should not be considered outdated
                isOutdated.Should().BeFalse("When the latest runtime files match the installed ones, IsRuntimeOutdated should return false");
            }
            finally
            {
                try { if (Directory.Exists(latestFolder)) Directory.Delete(latestFolder, recursive: true); } catch { }
            }
        }

        /// <summary>
        /// Verifies that IsLoaderOutdated detects outdated loader/runtime when latest folder contains
        /// assemblies with outdated SourceHash metadata. Uses embedded resources for outdated DLLs.
        /// </summary>
        [Test, Category("Revit"), Category("DebugOnly")]
        public void IsLoaderOutdated_WhenLatestIsOutdated_ShouldReturnTrue()
        {
            var statusManager = LoaderApp.Instance?.AssemblyStatusManager;
            statusManager.Should().NotBeNull();

            var installedPath = LoaderConstants.LoaderAssemblyPath;
            File.Exists(installedPath).Should().BeTrue($"Installed loader assembly must exist at {installedPath}");
            var installedHash = AttributeMetadataLoader.TryGetFromFile(installedPath, BuildConstants.SourceHashMetadataKey);
            installedHash.Should().NotBe(AttributeMetadataLoader.MissingMarker, "Installed loader assembly must contain SourceHash metadata for this integration test");

            var stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var latestFolder = Path.Combine(LoaderConstants.RevitAddinDir, stamp);
            Directory.CreateDirectory(latestFolder);
            var stampFile = Path.Combine(LoaderConstants.RevitAddinDir, "Timestamp.txt");
            var oldStamp = File.Exists(stampFile) ? File.ReadAllText(stampFile) : null;
            File.WriteAllText(stampFile, Path.GetFileName(latestFolder));

            try
            {
                var resourceNames = _testAssembly.GetManifestResourceNames();
                foreach (var dll in LoaderConstants.LoaderAssemblies)
                {
                    var match = resourceNames.FirstOrDefault(r => r.EndsWith(dll, StringComparison.OrdinalIgnoreCase) && r.Contains("Resources.OutdatedDll"));
                    match.Should().NotBeNull($"Embedded resource for {dll} must exist as Resources.OutdatedDll.*");
                    var outPath = Path.Combine(latestFolder, dll);
                    using var stream = _testAssembly.GetManifestResourceStream(match!)!;
                    using var fs = File.Create(outPath);
                    stream.CopyTo(fs);
                }

                statusManager!.ProcessMsBuildSignal(latestFolder);

                var sampleLatestDll = Path.Combine(latestFolder, LoaderConstants.LoaderAssemblies[0]);
                var sampleLatestHash = AttributeMetadataLoader.TryGetFromFile(sampleLatestDll, BuildConstants.SourceHashMetadataKey);

                // Log installed vs latest hashes for each loader DLL
                foreach (var dll in LoaderConstants.LoaderAssemblies)
                {
                    var instPath = Path.Combine(LoaderConstants.RcaAddinDir, dll);
                    var instHash = AttributeMetadataLoader.TryGetFromFile(instPath, BuildConstants.SourceHashMetadataKey);
                    var latestPath = Path.Combine(latestFolder, dll);
                    var latestHash = AttributeMetadataLoader.TryGetFromFile(latestPath, BuildConstants.SourceHashMetadataKey);
                    TestLoggerInstance?.Log($"DLL={dll} installedHash={instHash} latestHash={latestHash}");
                }

                TestLoggerInstance?.Log($"Installed group sample hash={installedHash} Latest sample hash={sampleLatestHash}");

                installedHash.Should().NotBe(sampleLatestHash, "Test setup requires installed and latest hashes to differ");
                statusManager.IsLoaderOutdated().Should().BeTrue("Manager must detect outdated loader when hashes differ");
            }
            finally
            {
                try { if (oldStamp != null) File.WriteAllText(stampFile, oldStamp); else File.Delete(stampFile); } catch { }
                try { if (Directory.Exists(latestFolder)) Directory.Delete(latestFolder, recursive: true); } catch { }
            }
        }

        /// <summary>
        /// Verifies that IsRuntimeOutdated detects outdated runtime when latest folder contains
        /// assemblies with outdated SourceHash metadata. Uses embedded resources for outdated DLLs.
        /// </summary>
        [Test, Category("Revit"), Category("DebugOnly")]
        public void IsRuntimeOutdated_WhenLatestIsOutdated_ShouldReturnTrue()
        {
            var statusManager = LoaderApp.Instance?.AssemblyStatusManager;
            statusManager.Should().NotBeNull();

            var installedPath = Path.Combine(LoaderConstants.RcaAddinDir, LoaderConstants.RuntimeFileName);
            File.Exists(installedPath).Should().BeTrue($"Installed runtime assembly must exist at {installedPath}");
            var installedHash = AttributeMetadataLoader.TryGetFromFile(installedPath, BuildConstants.SourceHashMetadataKey);
            installedHash.Should().NotBe(AttributeMetadataLoader.MissingMarker, "Installed runtime assembly must contain SourceHash metadata for this integration test");

            var stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var latestFolder = Path.Combine(LoaderConstants.RevitAddinDir, stamp);
            Directory.CreateDirectory(latestFolder);
            var stampFile = Path.Combine(LoaderConstants.RevitAddinDir, "Timestamp.txt");
            var oldStamp = File.Exists(stampFile) ? File.ReadAllText(stampFile) : null;
            File.WriteAllText(stampFile, Path.GetFileName(latestFolder));

            try
            {
                var resourceNames = _testAssembly.GetManifestResourceNames();
                foreach (var dll in LoaderConstants.RuntimeAssemblies)
                {
                    var match = resourceNames.FirstOrDefault(r => r.EndsWith(dll, StringComparison.OrdinalIgnoreCase) && r.Contains("Resources.OutdatedDll"));
                    match.Should().NotBeNull($"Embedded resource for {dll} must exist as Resources.OutdatedDll.*");
                    var outPath = Path.Combine(latestFolder, dll);
                    using var stream = _testAssembly.GetManifestResourceStream(match!)!;
                    using var fs = File.Create(outPath);
                    stream.CopyTo(fs);
                }

                statusManager!.ProcessMsBuildSignal(latestFolder);

                var sampleLatestDll = Path.Combine(latestFolder, LoaderConstants.RuntimeFileName);
                var sampleLatestHash = AttributeMetadataLoader.TryGetFromFile(sampleLatestDll, BuildConstants.SourceHashMetadataKey);

                // Log installed vs latest hashes for each runtime DLL
                foreach (var dll in LoaderConstants.RuntimeAssemblies)
                {
                    var instPath = Path.Combine(LoaderConstants.RcaAddinDir, dll);
                    var instHash = AttributeMetadataLoader.TryGetFromFile(instPath, BuildConstants.SourceHashMetadataKey);
                    var latestPath = Path.Combine(latestFolder, dll);
                    var latestHash = AttributeMetadataLoader.TryGetFromFile(latestPath, BuildConstants.SourceHashMetadataKey);
                    TestLoggerInstance?.Log($"DLL={dll} installedHash={instHash} latestHash={latestHash}");
                }

                TestLoggerInstance?.Log($"Installed group sample hash={installedHash} Latest sample hash={sampleLatestHash}");

                installedHash.Should().NotBe(sampleLatestHash, "Test setup requires installed and latest hashes to differ");
                statusManager.IsRuntimeOutdated().Should().BeTrue("Manager must detect outdated runtime when hashes differ");
            }
            finally
            {
                try { if (oldStamp != null) File.WriteAllText(stampFile, oldStamp); else File.Delete(stampFile); } catch { }
                try { if (Directory.Exists(latestFolder)) Directory.Delete(latestFolder, recursive: true); } catch { }
            }
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

