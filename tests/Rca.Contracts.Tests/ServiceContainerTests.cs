using NUnit.Framework;
using FluentAssertions;
using Rca.Contracts.Infrastructure;
using System;

namespace Rca.Contracts.Tests
{
    /// <summary>
    /// Unit tests for ServiceContainer class.
    /// </summary>
    [TestFixture]
    public class ServiceContainerTests
    {
        private ServiceContainer container;

        [SetUp]
        public void Setup()
        {
            // Create a new instance for each test to ensure isolation
            container = new ServiceContainer();
        }

        [Test]
        [Category("Unit")]
        public void Register_WithValidInterface_ShouldRegisterService()
        {
            // Arrange
            var mockService = new TestService();

            // Act
            container.Register<ITestService>(mockService);

            // Assert
            container.IsRegistered<ITestService>().Should().BeTrue();
        }

        [Test]
        [Category("Unit")]
        public void Resolve_WithRegisteredService_ShouldReturnCorrectInstance()
        {
            // Arrange
            var mockService = new TestService();
            container.Register<ITestService>(mockService);

            // Act
            var result = container.Resolve<ITestService>();

            // Assert
            result.Should().BeSameAs(mockService);
        }

        [Test]
        [Category("Unit")]
        public void Resolve_WithUnregisteredService_ShouldThrowInvalidOperationException()
        {
            // Act & Assert
            Action act = () => container.Resolve<ITestService>();
            act.Should().Throw<InvalidOperationException>()
               .WithMessage("Service of type ITestService is not registered.");
        }

        [Test]
        [Category("Unit")]
        public void IsRegistered_WithUnregisteredService_ShouldReturnFalse()
        {
            // Act & Assert
            container.IsRegistered<ITestService>().Should().BeFalse();
        }

        [Test]
        [Category("Unit")]
        public void Register_WithSameInterfaceTwice_ShouldReplaceService()
        {
            // Arrange
            var firstService = new TestService();
            var secondService = new TestService();

            // Act
            container.Register<ITestService>(firstService);
            container.Register<ITestService>(secondService);

            // Assert
            var result = container.Resolve<ITestService>();
            result.Should().BeSameAs(secondService);
        }
    }

    /// <summary>
    /// Test interface for ServiceContainer tests.
    /// </summary>
    public interface ITestService
    {
        string GetValue();
    }

    /// <summary>
    /// Test implementation for ServiceContainer tests.
    /// </summary>
    public class TestService : ITestService
    {
        public string GetValue() => "Test";
    }
}