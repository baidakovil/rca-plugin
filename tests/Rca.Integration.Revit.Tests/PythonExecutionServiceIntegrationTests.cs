using NUnit.Framework;
using FluentAssertions;
using Rca.Core.Services;
using Autodesk.Revit.UI;
using System.Threading.Tasks;
using ricaun.RevitTest.TestAdapter;
using Autodesk.Revit.ApplicationServices;
using System;

namespace Rca.Integration.Revit.Tests
{
    // Base class to receive UIApplication from the ricaun test adapter (pattern from sample)
    public abstract class UIApplicationTests
    {
        protected UIApplication? uiapp;

        [OneTimeSetUp]
        public void GlobalSetup(UIApplication uiapp)
        {
            this.uiapp = uiapp;
        }
    }

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
        public async Task ExecuteAsync_EmptyCode_ReturnsEmpty()
        {
            // Act
            var result = await pythonService!.ExecuteAsync("");

            // Assert
            result.Should().BeEmpty();
        }

        [Test, Category("Revit")]
        public async Task ExecuteAsync_SimpleCode_ReturnsFormattedOutput()
        {
            // Arrange
            var code = "print('Hello from Python in Revit')";

            // Act
            var result = await pythonService!.ExecuteAsync(code);

            // Assert
            result.Should().Contain("PYTHON EXECUTION START");
            result.Should().Contain("Hello from Python in Revit");
            result.Should().Contain("PYTHON EXECUTION END");
        }

        [Test, Category("Revit")]
        public async Task ExecuteAsync_RevitApiCode_AccessesRevitContext()
        {
            // Arrange
            var code = "print(f'Document title: {doc.Title}')";

            // Act
            var result = await pythonService!.ExecuteAsync(code);

            // Assert
            result.Should().Contain("Document title:");
            result.Should().NotContain("Error");
        }
    }
}