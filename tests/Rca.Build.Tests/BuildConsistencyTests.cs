using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using NUnit.Framework;

namespace Rca.Build.Tests
{
    /// <summary>
    /// Tests to verify build consistency and hash integrity for deployed assemblies.
    /// These tests ensure that the source hash embedded in DLL metadata matches
    /// the hash written to SourceHash-*.txt version files.
    /// </summary>
    [TestFixture]
    public class BuildConsistencyTests
    {
        private string _runtimeDeployRoot = string.Empty;
        private string _latestDeployFolder = string.Empty;

        [SetUp]
        public void Setup()
        {
            _runtimeDeployRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RCA", "Runtime");

            if (!Directory.Exists(_runtimeDeployRoot))
            {
                Assert.Fail($"Runtime deploy root not found: {_runtimeDeployRoot}. " +
                           "Run 'dotnet build' on Rca.Loader and Rca.Runtime projects first.");
            }

            // Get latest timestamped folder
            var dirs = Directory.GetDirectories(_runtimeDeployRoot)
                .OrderByDescending(d => Path.GetFileName(d))
                .ToArray();

            if (dirs.Length == 0)
            {
                Assert.Fail($"No deploy folders found in {_runtimeDeployRoot}. " +
                           "Run 'dotnet build' on Rca.Loader and Rca.Runtime projects first.");
            }

            _latestDeployFolder = dirs[0];
            TestContext.WriteLine($"Testing deployment folder: {_latestDeployFolder}");
        }

        /// <summary>
        /// Verifies that the SourceHash embedded in Rca.Loader.dll metadata
        /// matches the hash value in the SourceHash-Loader-*.txt file.
        /// 
        /// Why this matters:
        /// - The version file is used for quick version checks without loading assemblies
        /// - The embedded metadata is used by AssemblyStatusManager at runtime
        /// - Mismatch indicates build process error (likely in MSBuild targets)
        /// </summary>
        [Test]
        public void LoaderDll_SourceHashMetadata_MatchesVersionFile()
        {
            // Arrange
            var loaderDll = Path.Combine(_latestDeployFolder, "Rca.Loader.dll");
            Assert.That(File.Exists(loaderDll), Is.True,
                $"Rca.Loader.dll not found in {_latestDeployFolder}");

            // Find SourceHash-Loader-*.txt file
            var versionFiles = Directory.GetFiles(_latestDeployFolder, "SourceHash-Loader-*.txt");
            Assert.That(versionFiles, Has.Length.EqualTo(1),
                $"Expected exactly one SourceHash-Loader-*.txt file, found {versionFiles.Length}");

            var versionFile = versionFiles[0];
            var hashFromFile = File.ReadAllText(versionFile).Trim();
            
            // Extract expected hash from filename: SourceHash-Loader-{hash}.txt
            var fileNameHash = Path.GetFileNameWithoutExtension(versionFile)
                .Replace("SourceHash-Loader-", string.Empty);

            TestContext.WriteLine($"Hash from version file content: {hashFromFile}");
            TestContext.WriteLine($"Hash from version file name: {fileNameHash}");

            // Verify file content matches filename
            Assert.That(hashFromFile, Is.EqualTo(fileNameHash),
                "Version file content doesn't match filename - file may be corrupted");

            // Act - Read metadata from DLL using Mono.Cecil
            var hashFromDll = ReadAssemblyMetadata(loaderDll, "SourceHash");

            TestContext.WriteLine($"Hash from DLL metadata: {hashFromDll}");

            // Assert
            Assert.That(hashFromDll, Is.Not.Null.And.Not.Empty,
                "SourceHash metadata not found in Rca.Loader.dll - AttributeInjector may have failed");

            Assert.That(hashFromDll, Is.EqualTo(hashFromFile),
                $"Hash mismatch!\n" +
                $"  Version file ({Path.GetFileName(versionFile)}): {hashFromFile}\n" +
                $"  DLL metadata (AssemblyMetadata[\"SourceHash\"]): {hashFromDll}\n" +
                $"This indicates a bug in the build process - likely in DeployLoaderToTemp or InjectLoaderAttributes targets.");
        }

        /// <summary>
        /// Verifies that the SourceHash embedded in Rca.Runtime.dll metadata
        /// matches the hash value in the SourceHash-Runtime-*.txt file.
        /// </summary>
        [Test]
        public void RuntimeDll_SourceHashMetadata_MatchesVersionFile()
        {
            // Arrange
            var runtimeDll = Path.Combine(_latestDeployFolder, "Rca.Runtime.dll");
            Assert.That(File.Exists(runtimeDll), Is.True,
                $"Rca.Runtime.dll not found in {_latestDeployFolder}");

            // Find SourceHash-Runtime-*.txt file
            var versionFiles = Directory.GetFiles(_latestDeployFolder, "SourceHash-Runtime-*.txt");
            Assert.That(versionFiles, Has.Length.EqualTo(1),
                $"Expected exactly one SourceHash-Runtime-*.txt file, found {versionFiles.Length}");

            var versionFile = versionFiles[0];
            var hashFromFile = File.ReadAllText(versionFile).Trim();

            // Extract expected hash from filename
            var fileNameHash = Path.GetFileNameWithoutExtension(versionFile)
                .Replace("SourceHash-Runtime-", string.Empty);

            TestContext.WriteLine($"Hash from version file content: {hashFromFile}");
            TestContext.WriteLine($"Hash from version file name: {fileNameHash}");

            // Verify file content matches filename
            Assert.That(hashFromFile, Is.EqualTo(fileNameHash),
                "Version file content doesn't match filename - file may be corrupted");

            // Act - Read metadata from DLL
            var hashFromDll = ReadAssemblyMetadata(runtimeDll, "SourceHash");

            TestContext.WriteLine($"Hash from DLL metadata: {hashFromDll}");

            // Assert
            Assert.That(hashFromDll, Is.Not.Null.And.Not.Empty,
                "SourceHash metadata not found in Rca.Runtime.dll - AttributeInjector may have failed");

            Assert.That(hashFromDll, Is.EqualTo(hashFromFile),
                $"Hash mismatch!\n" +
                $"  Version file ({Path.GetFileName(versionFile)}): {hashFromFile}\n" +
                $"  DLL metadata (AssemblyMetadata[\"SourceHash\"]): {hashFromDll}\n" +
                $"This indicates a bug in the build process.");
        }

        /// <summary>
        /// Verifies that DeployFolder metadata in both DLLs matches the actual folder timestamp.
        /// </summary>
        [Test]
        public void DeployedDlls_DeployFolderMetadata_MatchesFolderName()
        {
            // Arrange
            var folderTimestamp = Path.GetFileName(_latestDeployFolder);
            var loaderDll = Path.Combine(_latestDeployFolder, "Rca.Loader.dll");
            var runtimeDll = Path.Combine(_latestDeployFolder, "Rca.Runtime.dll");

            Assert.That(File.Exists(loaderDll), Is.True);
            Assert.That(File.Exists(runtimeDll), Is.True);

            // Act
            var loaderDeployFolder = ReadAssemblyMetadata(loaderDll, "DeployFolder");
            var runtimeDeployFolder = ReadAssemblyMetadata(runtimeDll, "DeployFolder");

            TestContext.WriteLine($"Folder name: {folderTimestamp}");
            TestContext.WriteLine($"Loader DeployFolder metadata: {loaderDeployFolder}");
            TestContext.WriteLine($"Runtime DeployFolder metadata: {runtimeDeployFolder}");

            // Assert
            Assert.That(loaderDeployFolder, Is.EqualTo(folderTimestamp),
                $"Loader DeployFolder metadata doesn't match folder name");

            Assert.That(runtimeDeployFolder, Is.EqualTo(folderTimestamp),
                $"Runtime DeployFolder metadata doesn't match folder name");
        }

        /// <summary>
        /// Verifies that AssemblyInformationalVersion contains SourceHash and DeployFolder.
        /// This is the human-readable version visible in .NET Reflection (not Windows Explorer).
        /// 
        /// Why this matters:
        /// - Windows Explorer shows native Win32 resources (FileVersion), which Mono.Cecil cannot modify
        /// - AssemblyInformationalVersion is a .NET managed attribute that we CAN control
        /// - This is what AssemblyStatusManager reads at runtime via Reflection
        /// - Format: "DeployFolder: {timestamp}, SourceHash: {hash}"
        /// </summary>
        [Test]
        public void LoaderDll_InformationalVersion_ContainsHashAndDeployFolder()
        {
            // Arrange
            var loaderDll = Path.Combine(_latestDeployFolder, "Rca.Loader.dll");
            Assert.That(File.Exists(loaderDll), Is.True);

            // Get expected values
            var expectedDeployFolder = Path.GetFileName(_latestDeployFolder);
            var expectedHash = ReadAssemblyMetadata(loaderDll, "SourceHash");
            
            Assert.That(expectedHash, Is.Not.Null.And.Not.Empty,
                "SourceHash metadata missing - cannot verify InformationalVersion");

            // Act
            var infoVersion = ReadInformationalVersion(loaderDll);
            
            TestContext.WriteLine($"InformationalVersion: {infoVersion}");
            TestContext.WriteLine($"Expected DeployFolder: {expectedDeployFolder}");
            TestContext.WriteLine($"Expected SourceHash: {expectedHash}");

            // Assert
            Assert.That(infoVersion, Is.Not.Null.And.Not.Empty,
                "AssemblyInformationalVersion not found in Rca.Loader.dll");

            Assert.That(infoVersion, Does.Contain(expectedDeployFolder!),
                $"InformationalVersion doesn't contain DeployFolder timestamp.\n" +
                $"  Expected substring: {expectedDeployFolder}\n" +
                $"  Actual value: {infoVersion}");

            Assert.That(infoVersion, Does.Contain(expectedHash!),
                $"InformationalVersion doesn't contain SourceHash.\n" +
                $"  Expected substring: {expectedHash}\n" +
                $"  Actual value: {infoVersion}");
        }

        /// <summary>
        /// Verifies that AssemblyInformationalVersion in Runtime DLL contains SourceHash and DeployFolder.
        /// </summary>
        [Test]
        public void RuntimeDll_InformationalVersion_ContainsHashAndDeployFolder()
        {
            // Arrange
            var runtimeDll = Path.Combine(_latestDeployFolder, "Rca.Runtime.dll");
            Assert.That(File.Exists(runtimeDll), Is.True);

            // Get expected values
            var expectedDeployFolder = Path.GetFileName(_latestDeployFolder);
            var expectedHash = ReadAssemblyMetadata(runtimeDll, "SourceHash");
            
            Assert.That(expectedHash, Is.Not.Null.And.Not.Empty,
                "SourceHash metadata missing - cannot verify InformationalVersion");

            // Act
            var infoVersion = ReadInformationalVersion(runtimeDll);
            
            TestContext.WriteLine($"InformationalVersion: {infoVersion}");
            TestContext.WriteLine($"Expected DeployFolder: {expectedDeployFolder}");
            TestContext.WriteLine($"Expected SourceHash: {expectedHash}");

            // Assert
            Assert.That(infoVersion, Is.Not.Null.And.Not.Empty,
                "AssemblyInformationalVersion not found in Rca.Runtime.dll");

            Assert.That(infoVersion, Does.Contain(expectedDeployFolder!),
                $"InformationalVersion doesn't contain DeployFolder timestamp.\n" +
                $"  Expected substring: {expectedDeployFolder}\n" +
                $"  Actual value: {infoVersion}");

            Assert.That(infoVersion, Does.Contain(expectedHash!),
                $"InformationalVersion doesn't contain SourceHash.\n" +
                $"  Expected substring: {expectedHash}\n" +
                $"  Actual value: {infoVersion}");
        }

        /// <summary>
        /// Helper method to read AssemblyMetadata attribute value from a DLL using Mono.Cecil.
        /// </summary>
        /// <param name="assemblyPath">Path to the assembly file.</param>
        /// <param name="key">Metadata key to read (e.g., "SourceHash", "DeployFolder").</param>
        /// <returns>Metadata value, or null if not found.</returns>
        private string? ReadAssemblyMetadata(string assemblyPath, string key)
        {
            try
            {
                var resolver = new DefaultAssemblyResolver();
                resolver.AddSearchDirectory(Path.GetDirectoryName(assemblyPath) ?? ".");

                var readerParams = new ReaderParameters
                {
                    AssemblyResolver = resolver,
                    InMemory = true,
                    ReadingMode = ReadingMode.Deferred
                };

                using var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, readerParams);

                var metadataAttr = assembly.CustomAttributes
                    .FirstOrDefault(ca =>
                        ca.AttributeType.FullName == "System.Reflection.AssemblyMetadataAttribute" &&
                        ca.ConstructorArguments.Count >= 2 &&
                        ca.ConstructorArguments[0].Value as string == key);

                if (metadataAttr != null && metadataAttr.ConstructorArguments.Count >= 2)
                {
                    return metadataAttr.ConstructorArguments[1].Value as string;
                }

                return null;
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"Error reading metadata from {assemblyPath}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Helper method to read AssemblyInformationalVersion from a DLL using Mono.Cecil.
        /// </summary>
        /// <param name="assemblyPath">Path to the assembly file.</param>
        /// <returns>InformationalVersion string, or null if not found.</returns>
        private string? ReadInformationalVersion(string assemblyPath)
        {
            try
            {
                var resolver = new DefaultAssemblyResolver();
                resolver.AddSearchDirectory(Path.GetDirectoryName(assemblyPath) ?? ".");

                var readerParams = new ReaderParameters
                {
                    AssemblyResolver = resolver,
                    InMemory = true,
                    ReadingMode = ReadingMode.Deferred
                };

                using var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, readerParams);

                // AssemblyInformationalVersion is stored in AssemblyAttributes
                var informationalVersionAttr = assembly.CustomAttributes
                    .FirstOrDefault(ca => ca.AttributeType.FullName == "System.Reflection.AssemblyInformationalVersionAttribute");

                return informationalVersionAttr?.ConstructorArguments[0].Value as string;
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"Error reading informational version from {assemblyPath}: {ex.Message}");
                throw;
            }
        }
    }
}
