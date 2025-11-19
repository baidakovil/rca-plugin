using NUnit.Framework;
using FluentAssertions;
using NSubstitute;
using Rca.UI.ViewModels;
using Rca.Contracts;
using System;
using System.Threading.Tasks;

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
      const string script = "some code";
      const string result = "ok";
      python.ExecuteSync(script).Returns(result);

      // Revit context provider returns null to avoid Revit dependency in unit tests
      Func<object?> nullContextProvider = () => null;

      var vm = new RcaDockablePanelViewModel(
          nullContextProvider,
          python);

      vm.InputText = script;

      // Act
      ((RelayCommand)vm.ExecutePythonCommand).Execute(null);

      // Wait a bit for the async operation to complete
      await Task.Delay(100);

      // Assert
      python.Received(1).ExecuteSync(script);
      vm.OutputText.Should().Be(result);
      vm.InputText.Should().BeEmpty();
    }
  }
}
