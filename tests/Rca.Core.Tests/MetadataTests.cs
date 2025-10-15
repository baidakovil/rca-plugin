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
        private static string GetLatestRuntimeFolder()
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RCA", "Runtime");
            if (!Directory.Exists(root)) return string.Empty;
            return Directory.GetDirectories(root).OrderByDescending(d => d).FirstOrDefault() ?? string.Empty;
        }

        [Test]
        public void Runtime_Assembly_Has_SourceHash_Metadata()
        {
            var latest = GetLatestRuntimeFolder();
            if (string.IsNullOrEmpty(latest)) Assert.Inconclusive("No runtime deploy folders");
            var runtimePath = Path.Combine(latest, LoaderConstants.RuntimeFileName);
            if (!File.Exists(runtimePath)) Assert.Inconclusive($"{LoaderConstants.RuntimeFileName} not found in latest deploy");

            var hash = AttributeMetadataLoader.TryGetFromFile(runtimePath, BuildConstants.SourceHashMetadataKey);

            TestContext.WriteLine($"Runtime Hash: {hash}");
            Assert.That(string.IsNullOrEmpty(hash) || hash == AttributeMetadataLoader.MissingMarker, Is.False, "SourceHash should be present in runtime assembly");
        }

        [Test]
        public void Loader_Group_Assemblies_Have_Metadata_In_LatestRuntimeFolder()
        {
            var latest = GetLatestRuntimeFolder();
            if (string.IsNullOrEmpty(latest)) Assert.Inconclusive("No runtime deploy folders");

            foreach (var dll in LoaderConstants.LoaderAssemblies)
            {
                var path = Path.Combine(latest, dll);
                Assert.That(File.Exists(path), Is.True, $"{dll} not found in latest deploy folder");

                var hash = AttributeMetadataLoader.TryGetFromFile(path, BuildConstants.SourceHashMetadataKey);
                var deploy = AttributeMetadataLoader.TryGetFromFile(path, BuildConstants.DeployFolderMetadataKey);

                TestContext.WriteLine($"{dll} Hash: {hash} Deploy: {deploy}");
                Assert.That(string.IsNullOrEmpty(hash) || hash == AttributeMetadataLoader.MissingMarker, Is.False, $"SourceHash missing in {dll}");
                Assert.That(string.IsNullOrEmpty(deploy) || deploy == AttributeMetadataLoader.MissingMarker, Is.False, $"DeployFolder missing in {dll}");
            }
        }
    }
}
