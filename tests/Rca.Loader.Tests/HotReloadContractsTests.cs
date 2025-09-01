using Rca.Loader.Contracts;
using System;
using Xunit;

namespace Rca.Loader.Tests
{
    /// <summary>
    /// Tests for the hot reload contracts and protocol.
    /// </summary>
    public class HotReloadContractsTests
    {
        [Fact]
        public void PipeConstants_ShouldHaveCorrectPipeName()
        {
            // Arrange & Act
            var pipeName = PipeConstants.PipeName;

            // Assert
            Assert.Equal("rca.hotreload", pipeName);
        }

        [Fact]
        public void PipeMessage_ShouldInitializeWithTimestamp()
        {
            // Arrange & Act
            var message = new PipeMessage { Type = "TEST" };

            // Assert
            Assert.Equal("TEST", message.Type);
            Assert.True(message.Timestamp > DateTime.MinValue);
            Assert.True(message.Timestamp <= DateTime.UtcNow);
        }

        [Fact]
        public void CommandMessage_ShouldInheritFromPipeMessage()
        {
            // Arrange & Act
            var command = new CommandMessage 
            { 
                Type = "COMMAND",
                Command = "RELOAD",
                Payload = new { test = "data" }
            };

            // Assert
            Assert.Equal("COMMAND", command.Type);
            Assert.Equal("RELOAD", command.Command);
            Assert.NotNull(command.Payload);
            Assert.True(command.Timestamp > DateTime.MinValue);
        }

        [Fact]
        public void EventMessage_ShouldInheritFromPipeMessage()
        {
            // Arrange & Act
            var eventMsg = new EventMessage 
            { 
                Type = "EVENT",
                Event = "RELOAD_DONE",
                Data = new { version = "1.0.0" }
            };

            // Assert
            Assert.Equal("EVENT", eventMsg.Type);
            Assert.Equal("RELOAD_DONE", eventMsg.Event);
            Assert.NotNull(eventMsg.Data);
        }

        [Fact]
        public void ReloadPayload_ShouldAllowFolderAndForceOptions()
        {
            // Arrange & Act
            var payload = new ReloadPayload 
            { 
                Folder = @"C:\test\folder",
                Force = true
            };

            // Assert
            Assert.Equal(@"C:\test\folder", payload.Folder);
            Assert.True(payload.Force);
        }

        [Fact]
        public void ErrorMessage_ShouldContainMessageAndException()
        {
            // Arrange & Act
            var error = new ErrorMessage 
            { 
                Type = "ERROR",
                Message = "Test error",
                Exception = "System.Exception: Test"
            };

            // Assert
            Assert.Equal("ERROR", error.Type);
            Assert.Equal("Test error", error.Message);
            Assert.Equal("System.Exception: Test", error.Exception);
        }

        [Fact]
        public void LogMessage_ShouldContainAllLogProperties()
        {
            // Arrange & Act
            var log = new LogMessage 
            { 
                Type = "LOG",
                Level = "Debug",
                Message = "Test log message",
                Source = "TestComponent"
            };

            // Assert
            Assert.Equal("LOG", log.Type);
            Assert.Equal("Debug", log.Level);
            Assert.Equal("Test log message", log.Message);
            Assert.Equal("TestComponent", log.Source);
        }
    }
}