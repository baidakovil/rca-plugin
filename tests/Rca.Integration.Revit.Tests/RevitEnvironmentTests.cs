using NUnit.Framework;
using Autodesk.Revit.ApplicationServices;
using System;
using Rca.Integration.Revit.Tests.Infrastructure;

namespace Rca.Integration.Revit.Tests
{
  /// <summary>
  /// Integration tests for Revit environment validation.
  /// 
  /// BUSINESS VALUE:
  /// - Validates Revit is running and accessible for integration tests
  /// - Ensures correct Revit version (2026) for API compatibility
  /// - Sanity check before running other integration tests
  /// 
  /// NOT TESTED (future work):
  /// - Revit language/locale settings
  /// - Installed Revit add-ins and conflicts
  /// - Revit performance/memory state
  /// - Document open/close state
  /// 
  /// WEAK POINTS:
  /// - RevitVersion_MatchesExpected: Magic string "2026" hardcoded instead of constant
  /// - Environment variable check (RCA_ENABLE_REVIT_TESTS) uses magic string
  /// - Only one test - minimal coverage of Revit environment
  /// - Test is skipped if env var not set, making it easy to miss issues
  /// - Uses old NUnit Assert.That syntax instead of FluentAssertions
  /// </summary>
  [TestFixture]
  public class RevitEnvironmentTests : UIApplicationTestsBase
  {
    private const string ExpectedVersion = "2026";

    /// <summary>
    /// Validates Revit 2026 is running. Uses magic strings and old Assert syntax.
    /// </summary>
    [Test, Category("Revit")]
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
