using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rca.Loader.Infrastructure;

namespace Rca.Core.Tests
{
    [TestFixture]
    public class MetadataTests
    {
        [Test]
        public void Runtime_Assembly_Has_SourceHash_Metadata()
        {
            // Arrange: locate the latest runtime deploy dir (same as loader uses)
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RCA", "Runtime");
            if (!Directory.Exists(root)) Assert.Inconclusive("Runtime deploy root not found");
            var latest = Directory.GetDirectories(root).OrderByDescending(d => d).FirstOrDefault();
            if (latest == null) Assert.Inconclusive("No runtime deploy folders");
            var runtimePath = Path.Combine(latest, "Rca.Runtime.dll");
            if (!File.Exists(runtimePath)) Assert.Inconclusive("Rca.Runtime.dll not found in latest deploy");

            // Act
            var hash = AttributeMetadataLoader.TryGetFromFile(runtimePath, BuildConstants.SourceHashMetadataKey);

            // Assert
            TestContext.WriteLine($"Hash: {hash}");
            Assert.That(string.IsNullOrEmpty(hash) || hash == AttributeMetadataLoader.MissingMarker, Is.False, "SourceHash should be present in runtime assembly");
        }

        [Test]
        public void Loader_Assembly_Has_SourceHash_And_DeployFolder_Metadata()
        {
            var addinDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Autodesk", "Revit", "2026", "Addins", "Rca");
            var loaderPath = Path.Combine(addinDir, "Rca.Loader.dll");
            if (!File.Exists(loaderPath)) Assert.Inconclusive("Loader not deployed to addin dir");

            var hash = AttributeMetadataLoader.TryGetFromFile(loaderPath, BuildConstants.SourceHashMetadataKey);
            var deploy = AttributeMetadataLoader.TryGetFromFile(loaderPath, BuildConstants.DeployFolderMetadataKey);
            TestContext.WriteLine($"Loader Hash: {hash} Deploy: {deploy}");
            Assert.That(string.IsNullOrEmpty(hash) || hash == AttributeMetadataLoader.MissingMarker, Is.False);
            Assert.That(string.IsNullOrEmpty(deploy) || deploy == AttributeMetadataLoader.MissingMarker, Is.False);
        }
    }
}
