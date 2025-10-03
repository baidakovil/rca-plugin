using NUnit.Framework;
using FluentAssertions;
using NSubstitute;
using Rca.UI.ViewModels;
using Rca.Contracts;
using System;
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
            python.ExecuteSync(Arg.Any<string>()).Returns("ok"); // Changed to ExecuteSync
            
            // Explicitly define a function that returns null UIApplication
            Func<UIApplication?> nullUiAppProvider = () => null;
            
            var vm = new RcaDockablePanelViewModel(
                nullUiAppProvider,
                python);
            
            vm.InputText = "print('hi')";

            // Act
            ((RelayCommand)vm.ExecutePythonCommand).Execute(null);
            
            // Wait a bit for the async operation to complete
            await Task.Delay(100);

            // Assert
            python.Received(1).ExecuteSync(Arg.Is<string>(s => s.Contains("print"))); // Changed to ExecuteSync
            vm.OutputText.Should().Contain("ok");
            vm.InputText.Should().BeEmpty();
        }
    }
}
