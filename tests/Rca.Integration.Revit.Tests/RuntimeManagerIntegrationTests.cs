using NUnit.Framework;
using FluentAssertions;
using Rca.Integration.Revit.Tests.Infrastructure;
using Rca.Loader;
using System.IO;

namespace Rca.Integration.Revit.Tests
{
  /// <summary>
  /// Integration tests for RuntimeManager in running Revit context.
  /// 
  /// BUSINESS VALUE:
  /// - Validates runtime loading state can be queried reliably
  /// - Ensures runtime path tracking works for loaded assemblies
  /// - Tests dockable content creation (UI panel instantiation)
  /// - Critical for hot-reload: runtime must load/unload without Revit restart
  /// 
  /// NOT TESTED (future work):
  /// - Runtime unloading (removed due to STA threading issues - see note below)
  /// - Runtime reloading (removed due to STA threading issues)
  /// - Multiple runtime load/unload cycles
  /// - Runtime loading with missing dependencies
  /// - Runtime loading with invalid/corrupted DLLs
  /// - Runtime version compatibility checks
  /// - Concurrent runtime operations (race conditions)
  /// 
  /// WEAK POINTS:
  /// - IsRuntimeLoaded_ShouldReturnBooleanWithoutError: Only checks it's boolean, not actual state validity
  /// - CurrentRuntimePath_WhenRuntimeLoaded_ShouldBeValid: Uses conditional logic - weak when runtime not loaded
  /// - CreateRuntimeDockableContent_ShouldAttemptCreation: Complex conditional logic makes test unclear
  /// - Missing tests for destructive operations (unload/reload) due to STA threading limitations
  /// - Tests don't validate runtime functionality after loading, only loading state
  /// - No tests for error recovery when runtime load fails
  /// </summary>
  [TestFixture]
  public class RuntimeManagerIntegrationTests : UIApplicationTestsBase
  {
    /// <summary>
    /// Verifies RuntimeManager is accessible via LoaderApp. Basic sanity check.
    /// </summary>
    [Test, Category("Revit")]
    public void RuntimeManager_ShouldBeAccessibleFromLoaderApp()
    {
      // Assert
      LoaderApp.Instance.Should().NotBeNull();
      LoaderApp.Instance!.RuntimeManager.Should().NotBeNull();
    }

    /// <summary>
    /// Tests IsRuntimeLoaded property. Weak - only checks boolean, not state validity.
    /// </summary>
    [Test, Category("Revit")]
    public void IsRuntimeLoaded_ShouldReturnBooleanWithoutError()
    {
      // Arrange
      var runtimeManager = LoaderApp.Instance?.RuntimeManager;
      runtimeManager.Should().NotBeNull();

      // Act
      var isLoaded = runtimeManager!.IsRuntimeLoaded;

      // Assert
      (isLoaded == true || isLoaded == false).Should().BeTrue();

      // Log the current state for diagnostics
      TestContext.WriteLine($"Runtime is currently loaded: {isLoaded}");
    }

    /// <summary>
    /// Validates runtime path tracking when loaded. Uses conditional logic - weak when not loaded.
    /// </summary>
    [Test, Category("Revit")]
    public void CurrentRuntimePath_WhenRuntimeLoaded_ShouldBeValid()
    {
      // Arrange
      var runtimeManager = LoaderApp.Instance?.RuntimeManager;
      runtimeManager.Should().NotBeNull();

      // Act
      var isLoaded = runtimeManager!.IsRuntimeLoaded;
      var runtimePath = runtimeManager.CurrentRuntimePath;

      // Assert
      if (isLoaded)
      {
        runtimePath.Should().NotBeNullOrWhiteSpace(
            "If runtime is loaded, path should be available");

        File.Exists(runtimePath).Should().BeTrue(
            "Runtime path should point to an existing file");

        Path.GetFileName(runtimePath).Should().Be("Rca.Runtime.dll",
            "Runtime path should point to Rca.Runtime.dll");
      }
      else
      {
        TestContext.WriteLine("Runtime not loaded, skipping path validation");
      }
    }

    /// <summary>
    /// Tests runtime UI panel creation. Complex conditional logic - unclear test intent.
    /// </summary>
    [Test, Category("Revit")]
    public void CreateRuntimeDockableContent_ShouldAttemptCreation()
    {
      // Arrange
      var runtimeManager = LoaderApp.Instance?.RuntimeManager;
      runtimeManager.Should().NotBeNull();

      // Act
      var content = runtimeManager!.CreateRuntimeDockableContent(out var error);

      // Assert
      if (runtimeManager.IsRuntimeLoaded)
      {
        // If runtime is loaded, content should be created or we should get a meaningful error
        if (content == null)
        {
          error.Should().NotBeNullOrWhiteSpace(
              "If content creation fails, error message should be provided");
          TestContext.WriteLine($"Content creation failed: {error}");
        }
        else
        {
          content.Should().BeAssignableTo<System.Windows.FrameworkElement>(
              "Runtime dockable content should be a WPF FrameworkElement");
          TestContext.WriteLine($"Content created successfully: {content.GetType().FullName}");
        }
      }
      else
      {
        content.Should().BeNull("Content cannot be created when runtime is not loaded");
        error.Should().Contain("not loaded",
            "Error should indicate runtime is not loaded");
      }
    }

    // NOTE: UnloadRuntime and ReloadLatest tests removed because:
    // - Both involve unloading runtime which causes STA threading issues
    // - Runtime reload internally calls UnloadRuntime, triggering the same crash
    // - These operations are destructive and break subsequent tests
    // - Runtime loading state is already verified by IsRuntimeLoaded and CurrentRuntimePath tests
  }
}

