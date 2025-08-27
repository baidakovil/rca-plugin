using NUnit.Framework;
using FluentAssertions;
using Rca.Core.Services;
using System.Linq;
using Rca.Contracts;

namespace Rca.Core.Tests
{
    [TestFixture]
    public class DebugLogServiceTests
    {
        [Test, Category("Unit")]
        public void LogInfo_AddsInfoEntry()
        {
            // Arrange
            var service = DebugLogService.Instance;
            var initialCount = service.Entries.Count;

            // Act
            service.LogInfo("hello");

            // Assert
            service.Entries.Count.Should().Be(initialCount + 1);
            service.Entries.Last().Type.Should().Be(DebugLogType.Info);
            service.Entries.Last().Message.Should().Contain("hello");
        }

        [Test, Category("Unit")]
        public void LogPythonOutput_AddsPythonOutputEntry()
        {
            // Arrange
            var service = DebugLogService.Instance;
            var initialCount = service.Entries.Count;

            // Act
            service.LogPythonOutput("py");

            // Assert
            service.Entries.Count.Should().Be(initialCount + 1);
            service.Entries.Last().Type.Should().Be(DebugLogType.PythonOutput);
        }
    }
}
