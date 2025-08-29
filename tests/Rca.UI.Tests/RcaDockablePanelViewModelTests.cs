using NUnit.Framework;
using FluentAssertions;
using NSubstitute;
using Rca.UI.ViewModels;
using Rca.Contracts;
using System.Threading.Tasks;
using Autodesk.Revit.UI;

namespace Rca.UI.Tests
{
    [TestFixture]
    public class RcaDockablePanelViewModelTests
    {
        [Test, Category("Unit")]
        public async Task ExecutePythonCommand_WithInput_CallsServiceAndClearsInput()
        {
            // Arrange
            var python = Substitute.For<IPythonExecutionService>();
            python.ExecuteAsync(Arg.Any<string>()).Returns(Task.FromResult("ok"));
            var vm = new RcaDockablePanelViewModel(
                () => null,
                python,
                () => null);
            vm.InputText = "print('hi')";

            // Act
            ((RelayCommand)vm.ExecutePythonCommand).Execute(null);
            
            // Wait a bit for the async operation to complete
            await Task.Delay(100);

            // Assert
            await python.Received(1).ExecuteAsync(Arg.Is<string>(s => s.Contains("print")));
            vm.OutputText.Should().Contain("ok");
            vm.InputText.Should().BeEmpty();
        }
    }
}
