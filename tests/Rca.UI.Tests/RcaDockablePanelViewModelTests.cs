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
      python.GetRuntimeStatus().Returns(PythonRuntimeStatus.Available(@"C:\\Python311\\python311.dll"));
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

    [Test, Category("Unit")]
    public async Task ExecutePythonCommand_WithoutPython_ShowsStatusAndDoesNotExecute()
    {
      var python = Substitute.For<IPythonExecutionService>();
      var status = PythonRuntimeStatus.MissingInstallation("Python 3.11 is not installed.");
      var promptShown = false;
      python.GetRuntimeStatus().Returns(status);

      var vm = new RcaDockablePanelViewModel(
          () => null,
          python,
          _ => promptShown = true);

      vm.InputText = "print('hello')";

      ((RelayCommand)vm.ExecutePythonCommand).Execute(null);

      await Task.Delay(50);

      python.DidNotReceive().ExecuteSync(Arg.Any<string>());
      vm.OutputText.Should().Be(status.Message);
      vm.InputText.Should().Be("print('hello')");
      promptShown.Should().BeTrue();
    }
  }
}
