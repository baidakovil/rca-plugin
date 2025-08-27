using NUnit.Framework;
using FluentAssertions;
using NSubstitute;
using RcaPlugin.Commands;
using Rca.Contracts;
using Rca.Contracts.Infrastructure;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System;

namespace RcaPlugin.Tests
{
    /// <summary>
    /// Unit tests for ShowDockablePanelCommand class.
    /// </summary>
    [TestFixture]
    public class ShowDockablePanelCommandTests
    {
        private ShowDockablePanelCommand command;
        private IRevitContext mockRevitContext;
        private ExternalCommandData mockCommandData;
        private UIApplication mockUIApplication;

        [SetUp]
        public void Setup()
        {
            command = new ShowDockablePanelCommand();
            
            // Setup mocks
            mockRevitContext = Substitute.For<IRevitContext>();
            mockUIApplication = Substitute.For<UIApplication>();
            mockCommandData = Substitute.For<ExternalCommandData>();
            mockCommandData.Application.Returns(mockUIApplication);

            // Register mock in service container
            var container = ServiceContainer.Instance;
            container.Register<IRevitContext>(mockRevitContext);
        }

        [Test]
        [Category("Unit")]
        public void Execute_WithValidCommandData_ShouldSetUIApplicationInRevitContext()
        {
            // Arrange
            string message = string.Empty;
            var elements = Substitute.For<ElementSet>();

            // Act
            var result = command.Execute(mockCommandData, ref message, elements);

            // Assert
            mockRevitContext.Received(1).CurrentUIApplication = mockUIApplication;
        }

        [Test]
        [Category("Unit")]
        public void Execute_WithValidCommandData_ShouldReturnSucceeded()
        {
            // Arrange
            string message = string.Empty;
            var elements = Substitute.For<ElementSet>();

            // Act
            var result = command.Execute(mockCommandData, ref message, elements);

            // Assert
            result.Should().Be(Result.Succeeded);
        }

        [Test]
        [Category("Unit")]
        public void Execute_WhenExceptionThrown_ShouldReturnFailed()
        {
            // Arrange
            string message = string.Empty;
            var elements = Substitute.For<ElementSet>();
            
            // Make the RevitContext throw an exception
            mockRevitContext
                .When(x => x.CurrentUIApplication = Arg.Any<object>())
                .Do(x => throw new InvalidOperationException("Test exception"));

            // Act
            var result = command.Execute(mockCommandData, ref message, elements);

            // Assert
            result.Should().Be(Result.Failed);
            message.Should().Be("Test exception");
        }
    }
}