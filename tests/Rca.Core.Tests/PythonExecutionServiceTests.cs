using NUnit.Framework;
using FluentAssertions;
using NSubstitute;
using Rca.Core.Services;
using Rca.Contracts;
using System;
using System.Threading.Tasks;

namespace Rca.Core.Tests
{
    /// <summary>
    /// Unit tests for PythonExecutionService class.
    /// </summary>
    [TestFixture]
    public class PythonExecutionServiceTests
    {
        private PythonExecutionService pythonService;

        [SetUp]
        public void Setup()
        {
            pythonService = new PythonExecutionService();
        }

        [Test]
        [Category("Unit")]
        public void SetRevitContext_WithValidContext_ShouldNotThrow()
        {
            // Arrange
            var mockUIApp = Substitute.For<object>();

            // Act & Assert
            Action act = () => pythonService.SetRevitContext(mockUIApp);
            act.Should().NotThrow();
        }

        [Test]
        [Category("Unit")]
        public void SetRevitContext_WithNullContext_ShouldNotThrow()
        {
            // Act & Assert
            Action act = () => pythonService.SetRevitContext(null);
            act.Should().NotThrow();
        }

        [Test]
        [Category("Unit")]
        public async Task ExecuteAsync_WithSimplePythonCode_ShouldReturnResult()
        {
            // Arrange
            const string pythonCode = "2 + 2";

            // Act
            var result = await pythonService.ExecuteAsync(pythonCode);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("4");
        }

        [Test]
        [Category("Unit")]
        public async Task ExecuteAsync_WithPrintStatement_ShouldCaptureOutput()
        {
            // Arrange
            const string pythonCode = "print('Hello, World!')";

            // Act
            var result = await pythonService.ExecuteAsync(pythonCode);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("Hello, World!");
        }

        [Test]
        [Category("Unit")]
        public async Task ExecuteAsync_WithInvalidPythonCode_ShouldReturnErrorMessage()
        {
            // Arrange
            const string invalidCode = "invalid syntax here!!!";

            // Act
            var result = await pythonService.ExecuteAsync(invalidCode);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("Error");
        }
    }
}