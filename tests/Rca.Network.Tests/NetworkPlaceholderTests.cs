using NUnit.Framework;
using FluentAssertions;

namespace Rca.Network.Tests
{
    [TestFixture]
    public class NetworkPlaceholderTests
    {
        [Test, Category("Unit"), Category("Smoke")]
        public void IsReady_Always_True()
        {
            // Act
            var ready = NetworkPlaceholder.IsReady;

            // Assert
            ready.Should().BeTrue();
        }
    }
}
