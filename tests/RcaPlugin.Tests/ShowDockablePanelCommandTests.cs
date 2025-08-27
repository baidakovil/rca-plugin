using NUnit.Framework;
using FluentAssertions;
using Rca.Contracts;
using NSubstitute;
using RcaPlugin.Commands;

namespace RcaPlugin.Tests
{
    [TestFixture]
    public class ShowDockablePanelCommandTests
    {
        private class DummyRevitContext : IRevitContext
        {
            public object CurrentUIApplication { get; set; }
        }

        [Test, Category("Unit")]
        public void UpdateContext_WithDummyUiApp_SetsValue()
        {
            // Arrange
            var ctx = new DummyRevitContext();
            var fakeUi = new object();

            // Act
            RevitContextHelper.UpdateContext(ctx, fakeUi);

            // Assert
            ctx.CurrentUIApplication.Should().BeSameAs(fakeUi);
        }
    }
}
