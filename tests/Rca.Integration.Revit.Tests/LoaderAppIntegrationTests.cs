using NUnit.Framework;
using FluentAssertions;
using Rca.Integration.Revit.Tests.Infrastructure;
using Rca.Loader;
using System;

namespace Rca.Integration.Revit.Tests
{
  /// <summary>
  /// Integration tests for LoaderApp functionality in running Revit context.
  /// 
  /// BUSINESS VALUE:
  /// - Verifies LoaderApp singleton is properly initialized when Revit starts
  /// - Ensures core components (RuntimeManager, AssemblyStatusManager, PanelHost) are available
  /// - Validates that LoaderApp has correct UIApplication reference for Revit API operations
  /// - These tests catch initialization failures that would break the entire add-in
  /// 
  /// NOT TESTED (future work):
  /// - LoaderApp shutdown/cleanup behavior when Revit exits
  /// - LoaderApp restart/reload scenarios
  /// - Error handling when components fail to initialize
  /// - Multi-instance/multi-document scenarios
  /// 
  /// WEAK POINTS:
  /// - LoaderApp_UIApplication_ShouldMatchTestContext: Brittle - assumes test context shares same UIApplication
  /// - All tests rely on static singleton (LoaderApp.Instance) - hard to isolate, not truly unit-testable
  /// - Tests don't verify component functionality, only presence (shallow validation)
  /// - LoaderApp_AssemblyStatusManager_ShouldHaveCurrentInfo: Checks nested properties exist but not their validity
  /// - No tests for LoaderApp behavior under error conditions (robust testing gap)
  /// </summary>
  [TestFixture]
  public class LoaderAppIntegrationTests : UIApplicationTestsBase
  {
    /// <summary>
    /// Verifies LoaderApp singleton is initialized. Critical test - if this fails, entire add-in is broken.
    /// </summary>
    [Test, Category("Revit")]
    public void LoaderApp_Instance_ShouldBeInitialized()
    {
      // Assert
      LoaderApp.Instance.Should().NotBeNull("LoaderApp should be initialized when Revit starts with the add-in");
    }

    /// <summary>
    /// Validates LoaderApp references the correct Revit UIApplication instance.
    /// </summary>
    [Test, Category("Revit")]
    public void LoaderApp_UIApplication_ShouldMatchTestContext()
    {
      // Assert
      LoaderApp.Instance.Should().NotBeNull();
      LoaderApp.Instance!.UIApplication.Should().NotBeNull();

      // Verify it's the same Revit instance
      // Use NUnit reference-equality assert to avoid FluentAssertions overload ambiguity
      Assert.That(LoaderApp.Instance!.UIApplication, Is.SameAs(uiapp),
          "LoaderApp should reference the same UIApplication instance as the test context");
    }

    /// <summary>
    /// Ensures RuntimeManager is available for runtime loading/unloading operations.
    /// </summary>
    [Test, Category("Revit")]
    public void LoaderApp_RuntimeManager_ShouldBeInitialized()
    {
      // Assert
      LoaderApp.Instance.Should().NotBeNull();
      LoaderApp.Instance!.RuntimeManager.Should().NotBeNull(
          "RuntimeManager should be initialized during LoaderApp startup");
    }

    /// <summary>
    /// Verifies AssemblyStatusManager is initialized for hot-reload tracking.
    /// </summary>
    [Test, Category("Revit")]
    public void LoaderApp_AssemblyStatusManager_ShouldBeInitialized()
    {
      // Assert
      LoaderApp.Instance.Should().NotBeNull();
      LoaderApp.Instance!.AssemblyStatusManager.Should().NotBeNull(
          "AssemblyStatusManager should be initialized during LoaderApp startup");
    }

    /// <summary>
    /// Validates AssemblyStatusManager has tracking info for loader and runtime assemblies.
    /// Shallow check - only verifies structure exists, not data validity.
    /// </summary>
    [Test, Category("Revit")]
    public void LoaderApp_AssemblyStatusManager_ShouldHaveCurrentInfo()
    {
      // Assert
      LoaderApp.Instance.Should().NotBeNull();
      var statusManager = LoaderApp.Instance!.AssemblyStatusManager;
      statusManager.Should().NotBeNull();

      var currentInfo = statusManager!.CurrentInfo;
      currentInfo.Should().NotBeNull();
      currentInfo.LoaderComponents.Should().NotBeNull();
      currentInfo.RuntimeAssembly.Should().NotBeNull();
      currentInfo.LoadedRuntimeAssembly.Should().NotBeNull();
      currentInfo.LastMSBuildSignal.Should().NotBeNull();
    }

    /// <summary>
    /// Checks that PanelHost is ready to display the dockable panel UI.
    /// </summary>
    [Test, Category("Revit")]
    public void LoaderApp_PanelHost_ShouldBeInitialized()
    {
      // Assert
      LoaderApp.Instance.Should().NotBeNull();
      LoaderApp.Instance!.PanelHost.Should().NotBeNull(
          "DockablePanel host should be initialized during LoaderApp startup");
    }
  }
}

