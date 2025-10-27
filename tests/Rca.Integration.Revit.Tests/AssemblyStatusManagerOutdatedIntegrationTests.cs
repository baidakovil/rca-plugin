using NUnit.Framework;
using FluentAssertions;
using Rca.Loader.AssemblyManagement;
using Rca.Loader.Infrastructure;
using System;
using System.IO;
using System.Linq;
using Rca.Loader;
using Rca.Generated;

namespace Rca.Integration.Revit.Tests
{
    /// <summary>
    /// Integration tests for loader/runtime outdated detection on <see cref="AssemblyStatusManager"/>.
    /// </summary>
    /// <remarks>
    /// These tests are marked as Revit integration tests because they rely on runtime
    /// environment artifacts (installed assemblies, generated build metadata and
    /// embedded outdated DLL resources). They use parameterized test cases to cover
    /// both loader and runtime flows in a DRY manner.
    /// </remarks>
    [TestFixture]
    [Category("Revit")]
    public class AssemblyStatusManagerOutdatedIntegrationTests
    {
        private string? _testLatestFolder;
        private readonly AssemblyStatusManager _statusManager = LoaderApp.Instance!.AssemblyStatusManager!;
        private readonly System.Reflection.Assembly _testAssembly = typeof(AssemblyStatusManagerOutdatedIntegrationTests).Assembly;

        /// <summary>
        /// Cleanup any temporary latest folder created during a test.
        /// </summary>
        [TearDown]
        public void Cleanup()
        {
            if (_testLatestFolder != null && Directory.Exists(_testLatestFolder))
            {
                try { Directory.Delete(_testLatestFolder, recursive: true); } catch { }
            }
        }

        /// <summary>
        /// Verifies that when the latest folder contains assemblies matching the installed
        /// ones, neither loader nor runtime are considered outdated.
        /// </summary>
        /// <param name="isRuntime">If <see langword="true"/>, test runtime flow; otherwise loader flow.</param>
        /// <param name="isOutdated">Unused parameter preserved for compatibility with previous test signatures.</param>
        [TestCase(false, false)]
        [TestCase(true, false)]
        public void IsOutdated_WhenLatestMatchesInstalled_ShouldReturnFalse(bool isRuntime, bool isOutdated)
        {
            // Arrange: set up latest folder with matching assemblies
            SetupLatestFolder(isRuntime, outdated: false);

            // Act
            bool result = isRuntime
                ? _statusManager.IsRuntimeOutdated()
                : _statusManager.IsLoaderOutdated();

            // Assert
            result.Should().BeFalse();
        }

        /// <summary>
        /// Verifies that when the latest folder contains deliberately outdated assemblies,
        /// the corresponding IsOutdated check returns <see langword="true"/>.
        /// </summary>
        /// <param name="isRuntime">If <see langword="true"/>, test runtime flow; otherwise loader flow.</param>
        /// <param name="isOutdated">Unused parameter preserved for compatibility with previous test signatures.</param>
        [TestCase(false, true)]
        [TestCase(true, true)]
        [Category("DebugOnly")]
        public void IsOutdated_WhenLatestIsOutdated_ShouldReturnTrue(bool isRuntime, bool isOutdated)
        {
            // Arrange: set up latest folder with outdated embedded resources
            SetupLatestFolder(isRuntime, outdated: true);

            // Act
            // For loader flow we simulate MSBuild signal handling which populates
            // the loaded runtime/loader records used by the comparison logic.
            if (!isRuntime)
                _statusManager.ProcessMsBuildSignal(_testLatestFolder!);

            bool result = isRuntime
                ? _statusManager.IsRuntimeOutdated()
                : _statusManager.IsLoaderOutdated();

            // Assert
            result.Should().BeTrue();
        }

        /// <summary>
        /// Creates a timestamped 'latest' folder and either copies the currently
        /// installed assemblies into it (matching scenario) or writes embedded
        /// outdated DLL resources into it (outdated scenario).
        /// </summary>
        /// <param name="isRuntime">If <see langword="true"/>, use runtime assemblies list; otherwise loader assemblies.</param>
        /// <param name="outdated">When <see langword="true"/>, use embedded outdated DLL resources.</param>
        private void SetupLatestFolder(bool isRuntime, bool outdated)
        {
            // Create unique timestamped folder under RuntimeDeployRoot
            var stamp = DateTime.Now.ToString(RcaBuildMetadata.TimestampPattern);
            _testLatestFolder = Path.Combine(LoaderConstants.RuntimeDeployRoot, stamp);
            Directory.CreateDirectory(_testLatestFolder);

            var assemblies = isRuntime
                ? LoaderConstants.RuntimeAssemblies
                : LoaderConstants.LoaderAssemblies;

            if (!outdated)
            {
                // Copy installed assemblies to latest folder
                var sourceDir = LoaderConstants.RcaAddinDir;
                foreach (var dll in assemblies)
                {
                    var src = Path.Combine(sourceDir, dll);
                    File.Exists(src).Should().BeTrue($"Required assembly {dll} must exist in {sourceDir}");
                    File.Copy(src, Path.Combine(_testLatestFolder, dll), overwrite: true);
                }
            }
            else
            {
                // Use embedded outdated DLL resources
                var resourcePrefix = "Resources.OutdatedDll";
                var resourceNames = _testAssembly.GetManifestResourceNames()
                    .Where(r => r.Contains(resourcePrefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                foreach (var dll in assemblies)
                {
                    var match = resourceNames.FirstOrDefault(r => r.EndsWith(dll, StringComparison.OrdinalIgnoreCase));
                    match.Should().NotBeNull($"Embedded outdated resource for {dll} must exist");
                    using var stream = _testAssembly.GetManifestResourceStream(match!)!;
                    using var fs = File.Create(Path.Combine(_testLatestFolder, dll));
                    stream.CopyTo(fs);
                }
            }
        }
    }
}
