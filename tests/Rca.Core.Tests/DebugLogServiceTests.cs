using NUnit.Framework;
using FluentAssertions;
using Rca.Core.Services;
using Rca.Contracts;
using System.Linq;

namespace Rca.Core.Tests
{
    /// <summary>
    /// Unit tests for DebugLogService class.
    /// </summary>
    [TestFixture]
    public class DebugLogServiceTests
    {
        private DebugLogService debugLogService;

        [SetUp]
        public void Setup()
        {
            // Use the singleton instance but clear any existing entries
            debugLogService = DebugLogService.Instance;
            // Clear existing entries by creating a fresh static collection access
        }

        [Test]
        [Category("Unit")]
        public void LogInfo_WithMessage_ShouldAddInfoEntry()
        {
            // Arrange
            const string testMessage = "Test info message";
            var initialCount = debugLogService.Entries.Count;

            // Act
            debugLogService.LogInfo(testMessage);

            // Assert
            debugLogService.Entries.Count.Should().Be(initialCount + 1);
            var lastEntry = debugLogService.Entries.Last();
            lastEntry.Type.Should().Be(DebugLogType.Info);
            lastEntry.Message.Should().Contain(testMessage);
        }

        [Test]
        [Category("Unit")]
        public void LogError_WithMessage_ShouldAddErrorEntry()
        {
            // Arrange
            const string testMessage = "Test error message";
            var initialCount = debugLogService.Entries.Count;

            // Act
            debugLogService.LogError(testMessage);

            // Assert
            debugLogService.Entries.Count.Should().Be(initialCount + 1);
            var lastEntry = debugLogService.Entries.Last();
            lastEntry.Type.Should().Be(DebugLogType.Error);
            lastEntry.Message.Should().Contain(testMessage);
        }

        [Test]
        [Category("Unit")]
        public void LogPythonOutput_WithMessage_ShouldAddPythonOutputEntry()
        {
            // Arrange
            const string testMessage = "Test python output";
            var initialCount = debugLogService.Entries.Count;

            // Act
            debugLogService.LogPythonOutput(testMessage);

            // Assert
            debugLogService.Entries.Count.Should().Be(initialCount + 1);
            var lastEntry = debugLogService.Entries.Last();
            lastEntry.Type.Should().Be(DebugLogType.PythonOutput);
            lastEntry.Message.Should().Contain(testMessage);
        }

        [Test]
        [Category("Unit")]
        public void LogCustom_WithMessageAndType_ShouldAddCustomEntry()
        {
            // Arrange
            const string testMessage = "Test custom message";
            const DebugLogType customType = DebugLogType.Error;
            var initialCount = debugLogService.Entries.Count;

            // Act
            debugLogService.LogCustom(testMessage, customType);

            // Assert
            debugLogService.Entries.Count.Should().Be(initialCount + 1);
            var lastEntry = debugLogService.Entries.Last();
            lastEntry.Type.Should().Be(customType);
            lastEntry.Message.Should().Contain(testMessage);
        }

        [Test]
        [Category("Unit")]
        public void StaticLogInfo_WithMessage_ShouldWorkThroughStaticMethod()
        {
            // Arrange
            const string testMessage = "Static test message";
            var initialCount = debugLogService.Entries.Count;

            // Act
            DebugLogService.StaticLogInfo(testMessage);

            // Assert
            debugLogService.Entries.Count.Should().Be(initialCount + 1);
            var lastEntry = debugLogService.Entries.Last();
            lastEntry.Type.Should().Be(DebugLogType.Info);
            lastEntry.Message.Should().Contain(testMessage);
        }
    }
}