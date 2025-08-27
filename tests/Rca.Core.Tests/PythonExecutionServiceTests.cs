using NUnit.Framework;
using FluentAssertions;
using Rca.Core.Services;
using System.Threading.Tasks;

namespace Rca.Core.Tests
{
    [TestFixture]
    public class PythonExecutionServiceTests
    {
        [Test, Category("Unit")]
        public async Task ExecuteAsync_EmptyCode_ReturnsEmpty()
        {
            // Arrange
            var svc = new PythonExecutionService();
            // We intentionally do not set Revit context because we supply empty code (fast guard path)

            // Act
            var result = await svc.ExecuteAsync("");

            // Assert
            result.Should().BeEmpty();
        }
    }
}
