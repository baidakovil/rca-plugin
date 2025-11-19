using NUnit.Framework;
using FluentAssertions;
using Rca.Core.Services;
using Autodesk.Revit.UI;
using System.Threading.Tasks;
using Autodesk.Revit.ApplicationServices;
using System;
using Rca.Integration.Revit.Tests.Infrastructure;

namespace Rca.Integration.Revit.Tests
{
  /// <summary>
  /// Integration tests for PythonExecutionService with Revit API context.
  /// 
  /// BUSINESS VALUE:
  /// - Validates IronPython engine executes code in Revit context
  /// - Ensures Python code has access to Revit API objects (doc, uiapp)
  /// - Tests synchronous Python execution for scripting
  /// - Critical for AI assistant: must execute user Python commands safely
  /// 
  /// NOT TESTED (future work):
  /// - Async execution (ExecuteAsync) - inconclusive due to ExternalEvent limitation
  /// - Python syntax errors and exception handling
  /// - Python code modifying Revit model (transactions)
  /// - Python imports and module system
  /// - Timeout handling for long-running Python scripts
  /// - Python output capture (stdout/stderr separation)
  /// - Python code accessing external libraries
  /// 
  /// WEAK POINTS:
  /// - ExecuteSync_EmptyCode_ReturnsEmpty: Trivial test, more for coverage than value
  /// - ExecuteSync_RevitApiCode_AccessesRevitContext: Assumes doc.Title exists - brittle for empty documents
  /// - ExecuteAsync test is marked inconclusive - doesn't test real async behavior
  /// - Tests use magic strings ("Hello from Python") instead of constants
  /// - No tests for error cases (invalid Python syntax, runtime exceptions)
  /// - Tests don't verify Python scope isolation between executions
  /// </summary>
  [TestFixture]
  public class PythonExecutionServiceIntegrationTests : UIApplicationTestsBase
  {
    private PythonExecutionService? pythonService;

    [SetUp]
    public void Setup()
    {
      pythonService = new PythonExecutionService();
      if (uiapp != null)
      {
        pythonService.SetRevitContext(uiapp);
      }
    }

    /// <summary>
    /// Tests empty code execution. Trivial test - more for coverage than value.
    /// </summary>
    [Test, Category("Revit")]
    public void ExecuteSync_EmptyCode_ReturnsEmpty()
    {
      // Act
      var result = pythonService!.ExecuteSync("");

      // Assert
      result.Should().BeEmpty();
    }

    /// <summary>
    /// Validates basic Python execution and output capture. Uses magic string.
    /// </summary>
    [Test, Category("Revit")]
    public void ExecuteSync_SimpleCode_ReturnsOutput()
    {
      // Arrange.
      var code = "print('Hello from Python in Revit')";

      // Act
      var result = pythonService!.ExecuteSync(code);

      // Assert
      result.Should().Contain("Hello from Python in Revit");
      result.Should().NotContain("Error");
    }

    /// <summary>
    /// Tests Python access to Revit API objects. Brittle - assumes doc.Title exists.
    /// </summary>
    [Test, Category("Revit")]
    public void ExecuteSync_RevitApiCode_AccessesRevitContext()
    {
      // Arrange
      var code = "print(f'Document title: {doc.Title}')";

      // Act
      var result = pythonService!.ExecuteSync(code);

      // Assert
      result.Should().Contain("Document title:");
      result.Should().NotContain("Error");
    }

    /// <summary>
    /// Tests async Python execution. Always inconclusive due to ExternalEvent limitation - weak test.
    /// </summary>
    [Test, Category("Revit")]
    public async Task ExecuteAsync_SimpleCode_ReturnsOutput()
    {
      // This test verifies that async execution still works when called from within Revit API context
      try
      {
        // Arrange
        var code = "print('Hello from async Python in Revit')";

        // Act
        var result = await pythonService!.ExecuteAsync(code);

        // Assert
        result.Should().Contain("Hello from async Python in Revit");
        result.Should().Contain("Hello");
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("ExternalEvent"))
      {
        // This is expected when running in test context - ExternalEvent cannot be created outside of standard API execution
        Assert.Inconclusive("ExternalEvent cannot be created in test context - this is expected behavior");
      }
    }
  }
}
