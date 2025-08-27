using NUnit.Framework;
using FluentAssertions;
using Rca.Network;

namespace Rca.Network.Tests
{
    /// <summary>
    /// Unit tests for NetworkPlaceholder class.
    /// </summary>
    [TestFixture]
    public class NetworkPlaceholderTests
    {
        [Test]
        [Category("Unit")]
        public void IsReady_ShouldReturnTrue()
        {
            // Act
            var result = NetworkPlaceholder.IsReady;

            // Assert
            result.Should().BeTrue();
        }
    }
}