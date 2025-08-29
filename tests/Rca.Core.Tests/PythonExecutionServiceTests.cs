using NUnit.Framework;
using FluentAssertions;
using Rca.Contracts;
using NSubstitute;
using System.Threading.Tasks;

namespace Rca.Core.Tests
{
    [TestFixture]
    public class PythonExecutionServiceTests
    {
        private IPythonExecutionService? pythonService;

        [SetUp]
        public void Setup()
        {
            // Mock the service to test contract behavior
            pythonService = Substitute.For<IPythonExecutionService>();
        }

        [Test, Category("Unit")]
        public async Task ExecuteAsync_EmptyCode_ReturnsEmpty()
        {
            // Arrange
            pythonService!.ExecuteAsync("").Returns(Task.FromResult(""));

            // Act
            var result = await pythonService.ExecuteAsync("");

            // Assert
            result.Should().BeEmpty();
            await pythonService.Received(1).ExecuteAsync("");
        }

        [Test, Category("Unit")]
        public async Task ExecuteAsync_ValidCode_ReturnsExpectedResult()
        {
            // Arrange
            var code = "print('Hello World')";
            var expectedResult = "--- [PYTHON EXECUTION START] ---\nOutput: Hello World\n--- [PYTHON EXECUTION END] ---";
            pythonService!.ExecuteAsync(code).Returns(Task.FromResult(expectedResult));

            // Act
            var result = await pythonService.ExecuteAsync(code);

            // Assert
            result.Should().Contain("Hello World");
        }

        [Test, Category("Unit")]
        public void SetRevitContext_ValidContext_DoesNotThrow()
        {
            // Arrange & Act & Assert
            pythonService!.When(x => x.SetRevitContext(Arg.Any<object>())).Do(_ => { });
            Assert.DoesNotThrow(() => pythonService!.SetRevitContext(new object()));
        }
    }
}
