using NUnit.Framework;
using Rca.Loader.Infrastructure;
using System;
using System.IO;

namespace Rca.Loader.Tests
{
    /// <summary>
    /// Tests for <see cref="LoaderConstants"/> class.
    /// </summary>
    [TestFixture]
    public class LoaderConstantsTests
    {
        /// <summary>
        /// Verifies that runtime file name constant is correct.
        /// </summary>
        [Test]
        public void RuntimeFileName_ShouldHaveExpectedValue()
        {
            Assert.That(LoaderConstants.RuntimeFileName, Is.EqualTo("Rca.Runtime.dll"));
        }

        /// <summary>
        /// Verifies that loader file name constant is correct.
        /// </summary>
        [Test]
        public void LoaderFileName_ShouldHaveExpectedValue()
        {
            Assert.That(LoaderConstants.LoaderFileName, Is.EqualTo("Rca.Loader.dll"));
        }

        /// <summary>
        /// Verifies that pipe name constant is correct.
        /// </summary>
        [Test]
        public void CommandPipeName_ShouldHaveExpectedValue()
        {
            Assert.That(LoaderConstants.CommandPipeName, Is.EqualTo("RCA_COMMAND_PIPE"));
        }

        /// <summary>
        /// Verifies that RevitAddinsDir points to the Revit Addins folder under ApplicationData.
        /// </summary>
        [Test]
        public void RevitAddinsDir_ShouldPointToRevitAddinsUnderAppData()
        {
            var expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Autodesk",
                "Revit",
                "Addins",
                "2026");

            Assert.That(LoaderConstants.RevitAddinsDir, Is.EqualTo(expected));
        }

        /// <summary>
        /// Verifies that TestDeployRoot uses LocalApplicationData and correct path structure.
        /// </summary>
        [Test]
        public void TestDeployRoot_ShouldUseLocalApplicationData()
        {
            var expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RCA",
                "Test");

            Assert.That(LoaderConstants.TestDeployRoot, Is.EqualTo(expected));
        }

        /// <summary>
        /// Verifies that RevitAddinsDir points to the Revit Addins folder under ApplicationData (uses RcaRevitVersion).
        /// </summary>
        [Test]
        public void RevitAddinsDir_ShouldUseApplicationData()
        {
            var expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Autodesk",
                "Revit",
                "Addins",
                LoaderConstants.RevitVersion);

            Assert.That(LoaderConstants.RevitAddinsDir, Is.EqualTo(expected));
        }

        /// <summary>
        /// Verifies that RcaLoaderDir is either under the Revit addins root or is a valid absolute path
        /// (fallback to assembly location is allowed in test environment).
        /// </summary>
        [Test]
        public void RcaLoaderDir_ShouldBeUnderRevitAddinsDirOrBeAbsolute()
        {
            var revitRoot = LoaderConstants.RevitAddinsDir;
            var rcaDir = LoaderConstants.RcaLoaderDir;

            Assert.That(rcaDir, Is.Not.Null.And.Not.Empty, "RcaAddinDir must be set");

            if (rcaDir.StartsWith(revitRoot, StringComparison.OrdinalIgnoreCase))
            {
                // When deployed under Revit Addins, it must be a subdirectory of the Revit addins root
                Assert.That(rcaDir, Does.StartWith(revitRoot));
            }
            else
            {
                // In unit test runner the assembly location may be outside Revit addins; ensure it is an absolute path
                Assert.That(Path.IsPathRooted(rcaDir), Is.True, "RcaLoaderDir must be an absolute path when not under Revit addins");
            }
        }

        /// <summary>
        /// Verifies that LoaderAssemblyPath combines RcaLoaderDir and LoaderFileName.
        /// </summary>
        [Test]
        public void LoaderAssemblyPath_ShouldCombineDirectoryAndFileName()
        {
            var expected = Path.Combine(LoaderConstants.RcaLoaderDir, LoaderConstants.LoaderFileName);
            Assert.That(LoaderConstants.LoaderAssemblyPath, Is.EqualTo(expected));
        }

        /// <summary>
        /// Verifies that LoaderAssemblies array contains expected assemblies.
        /// </summary>
        [Test]
        public void LoaderAssemblies_ShouldContainExpectedAssemblies()
        {
            Assert.That(LoaderConstants.LoaderAssemblies, Has.Length.EqualTo(3));
            Assert.That(LoaderConstants.LoaderAssemblies, Contains.Item("Rca.Loader.dll"));
            Assert.That(LoaderConstants.LoaderAssemblies, Contains.Item("Rca.Loader.Contracts.dll"));
            Assert.That(LoaderConstants.LoaderAssemblies, Contains.Item("Rca.Logging.Contracts.dll"));
        }

        /// <summary>
        /// Verifies that RuntimeAssemblies array contains expected assemblies.
        /// </summary>
        [Test]
        public void RuntimeAssemblies_ShouldContainExpectedAssemblies()
        {
            Assert.That(LoaderConstants.RuntimeAssemblies, Has.Length.EqualTo(5));
            Assert.That(LoaderConstants.RuntimeAssemblies, Contains.Item("Rca.Runtime.dll"));
            Assert.That(LoaderConstants.RuntimeAssemblies, Contains.Item("Rca.Core.dll"));
            Assert.That(LoaderConstants.RuntimeAssemblies, Contains.Item("Rca.Network.dll"));
            Assert.That(LoaderConstants.RuntimeAssemblies, Contains.Item("Rca.UI.dll"));
            Assert.That(LoaderConstants.RuntimeAssemblies, Contains.Item("Rca.Contracts.dll"));
        }

        /// <summary>
        /// Verifies that loader and runtime assemblies don't overlap.
        /// </summary>
        [Test]
        public void LoaderAndRuntimeAssemblies_ShouldNotOverlap()
        {
            var loaderSet = new System.Collections.Generic.HashSet<string>(LoaderConstants.LoaderAssemblies);
            var runtimeSet = new System.Collections.Generic.HashSet<string>(LoaderConstants.RuntimeAssemblies);

            loaderSet.IntersectWith(runtimeSet);
            Assert.That(loaderSet, Is.Empty, "Loader and Runtime assemblies should not have common elements");
        }
    }
}

