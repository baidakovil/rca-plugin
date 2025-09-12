using NUnit.Framework;
using FluentAssertions;
using Rca.Core.Services;
using Autodesk.Revit.UI;
using System.Threading.Tasks;
using Autodesk.Revit.ApplicationServices;
using System;
using Rca.Loader.Testing;

namespace Rca.Integration.Revit.Tests
{
    [TestFixture]
    public class PythonExecutionServiceIntegrationTests : UIApplicationTests
    {
        private PythonExecutionService? pythonService;

        [SetUp]
        public void Setup()
        {
            pythonService = new PythonExecutionService();
            pythonService.SetRevitContext(uiapp);
        }

        [Test, Category("Revit")]
        public void ExecuteSync_EmptyCode_ReturnsEmpty()
        {
            // Act
            var result = pythonService!.ExecuteSync("");

            // Assert
            result.Should().BeEmpty();
        }

        [Test, Category("Revit")]
        public void ExecuteSync_SimpleCode_ReturnsFormattedOutput()
        {
            // Arrange
            var code = "print('Hello from Python in Revit')";

            // Act
            var result = pythonService!.ExecuteSync(code);

            // Assert
            result.Should().Contain("PYTHON EXECUTION START");
            result.Should().Contain("Hello from Python in Revit");
            result.Should().Contain("PYTHON EXECUTION END");
        }

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

        [Test, Category("Revit")]
        public async Task ExecuteSmartAsync_SimpleCode_ReturnsFormattedOutput()
        {
            // Arrange
            var code = "print('Hello from smart execution in Revit')";

            // Act
            var result = await pythonService!.ExecuteSmartAsync(code);

            // Assert
            result.Should().Contain("PYTHON EXECUTION START");
            result.Should().Contain("Hello from smart execution in Revit");
            result.Should().Contain("PYTHON EXECUTION END");
        }

        [Test, Category("Revit")]
        public async Task ExecuteSmartAsync_RevitApiCode_AccessesRevitContext()
        {
            // Arrange
            var code = "print(f'Smart execution - Document title: {doc.Title}')";

            // Act
            var result = await pythonService!.ExecuteSmartAsync(code);

            // Assert
            result.Should().Contain("Smart execution - Document title:");
            result.Should().NotContain("Error");
        }
    }
}