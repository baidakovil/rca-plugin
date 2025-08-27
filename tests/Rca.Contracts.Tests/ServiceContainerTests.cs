using NUnit.Framework;
using FluentAssertions;
using Rca.Contracts.Infrastructure;

namespace Rca.Contracts.Tests
{
    [TestFixture]
    public class ServiceContainerTests
    {
        private ServiceContainer container;

        [SetUp]
        public void SetUp()
        {
            container = ServiceContainer.Instance; // singleton, but we re-register inside tests
        }

        [Test, Category("Unit")]
        public void RegisterResolve_ServiceRegistered_ReturnsSameInstance()
        {
            // Arrange
            var sample = new object();

            // Act
            container.Register(sample);
            var resolved = container.Resolve<object>();

            // Assert
            resolved.Should().BeSameAs(sample);
        }

        [Test, Category("Unit")]
        public void Resolve_ServiceNotRegistered_Throws()
        {
            // Arrange / Act
            var act = () => container.Resolve<ServiceContainerTests>();

            // Assert
            act.Should().Throw<System.InvalidOperationException>();
        }
    }
}
