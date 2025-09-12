using NUnit.Framework;
using FluentAssertions;
using Rca.Core.Services;
using Rca.UI.ViewModels;
using Autodesk.Revit.UI;
using System.Threading.Tasks;
using System;
using Rca.Loader.Testing;

namespace Rca.Integration.Revit.Tests
{
    [TestFixture]
    public class PythonExecutionUIIntegrationTests : UIApplicationTests
    {
        private PythonExecutionService? pythonService;
        private RcaDockablePanelViewModel? viewModel;

        [SetUp]
        public void Setup()
        {
            pythonService = new PythonExecutionService();
            pythonService.SetRevitContext(uiapp);
            
            // Create the ViewModel with the real service to test actual UI execution path
            viewModel = new RcaDockablePanelViewModel(
                () => uiapp,                    // UIApplication provider
                pythonService,                  // Real Python service, not mocked
                () => null                      // Debug window factory (not used in this test)
            );
        }

        [Test, Category("Revit")]
        public async Task UI_ExecutePython_SmartExecution_WorksCorrectly()
        {
            // This test verifies that the UI uses smart execution correctly
            
            // Arrange
            var code = "print('Hello from smart UI execution path')";
            
            // Act: Execute via UI path (now uses ExecuteSmartAsync)
            viewModel!.InputText = code;
            
            // Get the command and execute it (simulates UI button click)
            var executeCommand = viewModel.ExecutePythonCommand;
            if (executeCommand.CanExecute(null))
            {
                executeCommand.Execute(null);
            }
            
            // Wait a bit for async operation to complete
            await Task.Delay(2000); // Give enough time for smart execution path selection
            
            var uiResult = viewModel.OutputText;
            
            // Assert
            uiResult.Should().NotBeNullOrEmpty("Smart UI execution should return a result");
            uiResult.Should().NotContain("Error", "Smart UI execution should not contain errors");
            uiResult.Should().Contain("Hello from smart UI execution path", "Smart UI execution should contain expected output");
            uiResult.Should().Contain("PYTHON EXECUTION START", "Smart UI execution should have proper formatting");
            uiResult.Should().Contain("PYTHON EXECUTION END", "Smart UI execution should have proper formatting");
        }

        [Test, Category("Revit")]
        public async Task UI_ExecutePython_RevitApiCode_AccessesRevitContext()
        {
            // This test verifies that UI execution can access Revit API objects
            
            // Arrange
            var code = "print(f'Document from UI: {doc.Title}')";
            
            // Act
            viewModel!.InputText = code;
            var executeCommand = viewModel.ExecutePythonCommand;
            if (executeCommand.CanExecute(null))
            {
                executeCommand.Execute(null);
            }
            
            // Wait for async operation
            await Task.Delay(2000);
            
            var result = viewModel.OutputText;
            
            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().NotContain("Error", $"UI execution should not contain errors. Actual result: {result}");
            result.Should().Contain("Document from UI:", "Should access Revit document from UI path");
        }

        [Test, Category("Revit")]
        public async Task UI_ExecutePython_InvalidCode_ReturnsErrorGracefully()
        {
            // This test verifies error handling in UI execution path
            
            // Arrange
            var invalidCode = "invalid_python_syntax(";
            
            // Act
            viewModel!.InputText = invalidCode;
            var executeCommand = viewModel.ExecutePythonCommand;
            if (executeCommand.CanExecute(null))
            {
                executeCommand.Execute(null);
            }
            
            // Wait for async operation
            await Task.Delay(2000);
            
            var result = viewModel.OutputText;
            
            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("ERROR", "Invalid code should produce error output");
        }

        [Test, Category("Revit")]
        public void UI_ExecutePython_CommandCanExecute_OnlyWhenInputNotEmpty()
        {
            // This test verifies the command can-execute logic
            
            // Arrange & Act & Assert
            viewModel!.InputText = "";
            viewModel.ExecutePythonCommand.CanExecute(null).Should().BeFalse("Command should not execute with empty input");
            
            viewModel.InputText = "print('test')";
            viewModel.ExecutePythonCommand.CanExecute(null).Should().BeTrue("Command should execute with valid input");
            
            viewModel.InputText = "   ";
            viewModel.ExecutePythonCommand.CanExecute(null).Should().BeFalse("Command should not execute with whitespace-only input");
        }

        [Test, Category("Revit")]
        public async Task UI_ExecutePython_MultipleSequentialExecutions_WorkCorrectly()
        {
            // This test verifies that multiple sequential executions through UI work correctly
            // This is important because ExternalEvent needs to be reusable
            
            // Arrange
            var codes = new[]
            {
                "print('First execution')",
                "print('Second execution')", 
                "print('Third execution')"
            };
            
            // Act & Assert
            for (int i = 0; i < codes.Length; i++)
            {
                viewModel!.InputText = codes[i];
                var executeCommand = viewModel.ExecutePythonCommand;
                
                if (executeCommand.CanExecute(null))
                {
                    executeCommand.Execute(null);
                }
                
                // Wait for completion
                await Task.Delay(2000);
                
                var result = viewModel.OutputText;
                result.Should().Contain($"{i + 1} execution", $"Execution {i + 1} should produce expected output");
                result.Should().NotContain("Error", $"Execution {i + 1} should not contain errors");
                
                // Input should be cleared after execution
                viewModel.InputText.Should().BeEmpty($"Input should be cleared after execution {i + 1}");
            }
        }
    }
}