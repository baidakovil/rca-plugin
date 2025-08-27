using NUnit.Framework;
using FluentAssertions;
using NSubstitute;
using Rca.UI.ViewModels;
using Rca.Contracts;
using Rca.UI.Views;
using Autodesk.Revit.UI;
using System;
using System.Threading.Tasks;

namespace Rca.UI.Tests
{
    /// <summary>
    /// Unit tests for RcaDockablePanelViewModel class.
    /// </summary>
    [TestFixture]
    public class RcaDockablePanelViewModelTests
    {
        private RcaDockablePanelViewModel viewModel;
        private IPythonExecutionService mockPythonService;
        private Func<UIApplication> mockUIAppProvider;
        private Func<DebugInfoWindow> mockDebugInfoWindowFactory;

        [SetUp]
        public void Setup()
        {
            mockPythonService = Substitute.For<IPythonExecutionService>();
            mockUIAppProvider = Substitute.For<Func<UIApplication>>();
            mockDebugInfoWindowFactory = Substitute.For<Func<DebugInfoWindow>>();

            viewModel = new RcaDockablePanelViewModel(
                mockUIAppProvider,
                mockPythonService,
                mockDebugInfoWindowFactory);
        }

        [Test]
        [Category("Unit")]
        public void Constructor_WithValidDependencies_ShouldInitializeCommands()
        {
            // Assert
            viewModel.ClickCommand.Should().NotBeNull();
            viewModel.ExecutePythonCommand.Should().NotBeNull();
            viewModel.ShowDebugInfoCommand.Should().NotBeNull();
        }

        [Test]
        [Category("Unit")]
        public void InputText_WhenSet_ShouldUpdateProperty()
        {
            // Arrange
            const string testInput = "test input";

            // Act
            viewModel.InputText = testInput;

            // Assert
            viewModel.InputText.Should().Be(testInput);
        }

        [Test]
        [Category("Unit")]
        public void OutputText_WhenSet_ShouldUpdateProperty()
        {
            // Arrange
            const string testOutput = "test output";

            // Act
            viewModel.OutputText = testOutput;

            // Assert
            viewModel.OutputText.Should().Be(testOutput);
        }

        [Test]
        [Category("Unit")]
        public void ClickCommand_WhenExecuted_ShouldNotThrow()
        {
            // Act & Assert
            Action act = () => viewModel.ClickCommand.Execute(null);
            act.Should().NotThrow();
        }

        [Test]
        [Category("Unit")]
        public async Task ExecutePythonCommand_WhenExecuted_ShouldCallPythonService()
        {
            // Arrange
            const string testCode = "print('test')";
            const string expectedResult = "test output";
            viewModel.InputText = testCode;
            mockPythonService.ExecuteAsync(testCode).Returns(expectedResult);

            // Act
            viewModel.ExecutePythonCommand.Execute(null);
            
            // Wait a bit for async operation
            await Task.Delay(100);

            // Assert
            await mockPythonService.Received(1).ExecuteAsync(testCode);
        }

        [Test]
        [Category("Unit")]
        public void ExecutePythonCommand_WithUIAppProvider_ShouldSetRevitContext()
        {
            // Arrange
            var mockUIApp = Substitute.For<UIApplication>();
            mockUIAppProvider.Invoke().Returns(mockUIApp);
            viewModel.InputText = "test code";

            // Act
            viewModel.ExecutePythonCommand.Execute(null);

            // Assert
            mockPythonService.Received(1).SetRevitContext(mockUIApp);
        }
    }
}