using NUnit.Framework;
using Autodesk.Revit.ApplicationServices;
using System;

namespace Rca.Integration.Revit.Tests
{
    [TestFixture]
    public class RevitEnvironmentTests
    {
        private const string ExpectedVersion = "2026";

        [Test, Category("Revit")]
        public void RevitVersion_MatchesExpected(Application app)
        {
            if (Environment.GetEnvironmentVariable("RCA_ENABLE_REVIT_TESTS") != "1")
                Assert.Ignore("Revit integration tests disabled (set RCA_ENABLE_REVIT_TESTS=1).");

            Assert.IsNotNull(app);
            Assert.AreEqual(ExpectedVersion, app.VersionNumber);
        }
    }
}
