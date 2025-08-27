using NUnit.Framework;
using Autodesk.Revit.ApplicationServices;
using System;

namespace Rca.Integration.Revit.Tests
{
    /// <summary>
    /// Integration tests for Revit environment validation.
    /// </summary>
    [TestFixture]
    public class RevitEnvironmentTests
    {
        private const string ExpectedVersion = "2026"; // обновлять при переходе

        [Test, Category("Revit")]
        public void RevitVersion_MatchesExpected(Application app)
        {
            if (Environment.GetEnvironmentVariable("RCA_ENABLE_REVIT_TESTS") != "1")
                Assert.Ignore("Revit integration tests disabled (set RCA_ENABLE_REVIT_TESTS=1).");

            Assert.IsNotNull(app);
            Assert.AreEqual(ExpectedVersion, app.VersionNumber);
        }

        [Test, Category("Revit")]
        public void RevitApplication_IsAvailable(Application app)
        {
            if (Environment.GetEnvironmentVariable("RCA_ENABLE_REVIT_TESTS") != "1")
                Assert.Ignore("Revit integration tests disabled (set RCA_ENABLE_REVIT_TESTS=1).");

            Assert.IsNotNull(app);
            Assert.IsNotNull(app.VersionName);
            Assert.IsNotEmpty(app.VersionName);
        }

        [Test, Category("Revit")]
        public void RevitApplication_HasExpectedProperties(Application app)
        {
            if (Environment.GetEnvironmentVariable("RCA_ENABLE_REVIT_TESTS") != "1")
                Assert.Ignore("Revit integration tests disabled (set RCA_ENABLE_REVIT_TESTS=1).");

            Assert.IsNotNull(app);
            Assert.IsNotNull(app.VersionBuild);
            Assert.IsNotNull(app.VersionNumber);
            Assert.IsNotNull(app.VersionName);
        }
    }
}