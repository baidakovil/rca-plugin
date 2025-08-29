using NUnit.Framework;
using FluentAssertions;
using Rca.Contracts;
using Rca.Contracts.Infrastructure;

namespace RcaPlugin.Tests
{
    [TestFixture]
    public class ShowDockablePanelCommandTests
    {
        private class DummyRevitContext : IRevitContext
        {
            public object CurrentUIApplication { get; set; } = new object();
        }

        [Test, Category("Unit")]
        public void DummyRevitContext_CurrentUIApplication_CanBeSetAndRetrieved()
        {
            // Arrange
            var context = new DummyRevitContext();
            var originalValue = context.CurrentUIApplication;
            var newValue = new object();

            // Act
            context.CurrentUIApplication = newValue;

            // Assert
            context.CurrentUIApplication.Should().BeSameAs(newValue);
            context.CurrentUIApplication.Should().NotBeSameAs(originalValue);
        }

        [Test, Category("Unit")]
        public void ServiceContainer_Integration_CanRegisterAndResolveRevitContext()
        {
            // Arrange
            var container = ServiceContainer.Instance;
            var dummyContext = new DummyRevitContext();
            IRevitContext? originalContext = null;
            
            // Try to get the original context, but don't fail if it doesn't exist
            try
            {
                originalContext = container.Resolve<IRevitContext>();
            }
            catch (System.InvalidOperationException)
            {
                // Service not registered, which is fine for this test
            }

            try
            {
                // Act
                container.Register<IRevitContext>(dummyContext);
                var resolvedContext = container.Resolve<IRevitContext>();

                // Assert
                resolvedContext.Should().BeSameAs(dummyContext);
            }
            finally
            {
                // Cleanup - restore original context if it existed
                if (originalContext != null)
                {
                    container.Register<IRevitContext>(originalContext);
                }
            }
        }

        [Test, Category("Unit")]
        public void ServiceContainer_Instance_IsNotNull()
        {
            // Act
            var container = ServiceContainer.Instance;

            // Assert
            container.Should().NotBeNull();
        }
    }
}
