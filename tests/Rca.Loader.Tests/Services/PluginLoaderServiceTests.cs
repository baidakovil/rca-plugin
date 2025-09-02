using NUnit.Framework;
using Rca.Contracts.Infrastructure;
using Rca.Loader.Services;
using System;
using System.IO;

namespace Rca.Loader.Tests.Services
{
    /// <summary>
    /// Tests for the PluginLoaderService class.
    /// </summary>
    [TestFixture]
    public class PluginLoaderServiceTests
    {
        private IPluginLoader pluginLoader;

        [SetUp]
        public void SetUp()
        {
            pluginLoader = new PluginLoaderService();
        }

        [Test]
        public void IsPluginLoaded_InitialState_ReturnsFalse()
        {
            // Arrange & Act & Assert
            Assert.IsFalse(pluginLoader.IsPluginLoaded);
        }

        [Test]
        public void LoadPlugin_NonExistentFile_ReturnsFalse()
        {
            // Arrange
            var nonExistentPath = Path.Combine(Path.GetTempPath(), "NonExistent.dll");
            bool loadingFailedCalled = false;
            string errorMessage = null;

            pluginLoader.LoadingFailed += (sender, error) =>
            {
                loadingFailedCalled = true;
                errorMessage = error;
            };

            // Act
            var result = pluginLoader.LoadPlugin(nonExistentPath);

            // Assert
            Assert.IsFalse(result);
            Assert.IsFalse(pluginLoader.IsPluginLoaded);
            Assert.IsTrue(loadingFailedCalled);
            Assert.IsTrue(errorMessage.Contains("not found"));
        }

        [Test]
        public void UnloadPlugin_NoPluginLoaded_ReturnsTrue()
        {
            // Arrange & Act
            var result = pluginLoader.UnloadPlugin();

            // Assert
            Assert.IsTrue(result);
            Assert.IsFalse(pluginLoader.IsPluginLoaded);
        }

        [Test]
        public void LoadingFailed_Event_IsRaisedWhenLoadFails()
        {
            // Arrange
            bool eventRaised = false;
            string receivedError = null;

            pluginLoader.LoadingFailed += (sender, error) =>
            {
                eventRaised = true;
                receivedError = error;
            };

            // Act
            pluginLoader.LoadPlugin("InvalidPath.dll");

            // Assert
            Assert.IsTrue(eventRaised);
            Assert.IsNotNull(receivedError);
        }
    }
}