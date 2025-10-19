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
        public void PipeName_ShouldHaveExpectedValue()
        {
            Assert.That(LoaderConstants.PipeName, Is.EqualTo("RCA_PIPE"));
        }

        /// <summary>
        /// Verifies that RuntimeDeployRoot uses LocalApplicationData and correct path structure.
        /// </summary>
        [Test]
        public void RuntimeDeployRoot_ShouldUseLocalApplicationData()
        {
            var expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RCA",
                "Runtime");

            Assert.That(LoaderConstants.RuntimeDeployRoot, Is.EqualTo(expected));
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
        /// Verifies that RevitAddinDir uses ApplicationData and correct Revit path.
        /// </summary>
        [Test]
        public void RevitAddinDir_ShouldUseApplicationData()
        {
            var expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Autodesk",
                "Revit",
                "Addins",
                "2026");

            Assert.That(LoaderConstants.RevitAddinDir, Is.EqualTo(expected));
        }

        /// <summary>
        /// Verifies that RcaAddinDir is subdirectory of RevitAddinDir.
        /// </summary>
        [Test]
        public void RcaAddinDir_ShouldBeSubdirectoryOfRevitAddinDir()
        {
            var expected = Path.Combine(LoaderConstants.RevitAddinDir, "Rca");
            Assert.That(LoaderConstants.RcaAddinDir, Is.EqualTo(expected));
        }

        /// <summary>
        /// Verifies that LoaderAssemblyPath combines RcaAddinDir and LoaderFileName.
        /// </summary>
        [Test]
        public void LoaderAssemblyPath_ShouldCombineDirectoryAndFileName()
        {
            var expected = Path.Combine(LoaderConstants.RcaAddinDir, LoaderConstants.LoaderFileName);
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

