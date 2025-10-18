using NUnit.Framework;
using Autodesk.Revit.ApplicationServices;
using System;
using Rca.Integration.Revit.Tests.Infrastructure;

namespace Rca.Integration.Revit.Tests
{
    [TestFixture]
    public class RevitEnvironmentTests : UIApplicationTestsBase
    {
        private const string ExpectedVersion = "2026";

        [Test]
        public void RevitVersion_MatchesExpected()
        {
            if (Environment.GetEnvironmentVariable("RCA_ENABLE_REVIT_TESTS") != "1")
                Assert.Ignore("Revit integration tests disabled (set RCA_ENABLE_REVIT_TESTS=1).");

            var app = uiapp?.Application;

            // Use NUnit 4.0.1 syntax
            Assert.That(app, Is.Not.Null);
            Assert.That(app!.VersionNumber, Is.EqualTo(ExpectedVersion));
        }
    }
}
