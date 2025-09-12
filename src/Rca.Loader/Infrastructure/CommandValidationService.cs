#nullable enable
using System;
using System.Text.Json;
using Rca.Loader.Services;
using Rca.Loader.Testing;

namespace Rca.Loader.Infrastructure
{
    /// <summary>
    /// Service for validating pipe commands and their payloads.
    /// </summary>
    public class CommandValidationService
    {
        /// <summary>
        /// Validates a pipe command and its payload.
        /// </summary>
        /// <param name="command">The command to validate.</param>
        /// <param name="validationError">The validation error message if validation fails.</param>
        /// <returns>True if the command is valid, otherwise false.</returns>
        public bool ValidateCommand(PipeCommand command, out string validationError)
        {
            if (command == null)
            {
                validationError = "Command cannot be null";
                return false;
            }

            if (string.IsNullOrWhiteSpace(command.Command))
            {
                validationError = "Command name cannot be empty";
                return false;
            }

            return command.Command.ToUpperInvariant() switch
            {
                PipeCommands.Reload => ValidateReloadCommand(command, out validationError),
                PipeCommands.Status => ValidateStatusCommand(command, out validationError),
                PipeCommands.RunTests => ValidateRunTestsCommand(command, out validationError),
                PipeCommands.TestInit => ValidateTestInitCommand(command, out validationError),
                _ => CreateUnknownCommandError(command.Command, out validationError)
            };
        }

        private static bool ValidateReloadCommand(PipeCommand command, out string validationError)
        {
            if (string.IsNullOrWhiteSpace(command.Payload))
            {
                validationError = "Reload command requires a valid folder path";
                return false;
            }

            validationError = string.Empty;
            return true;
        }

        private static bool ValidateStatusCommand(PipeCommand command, out string validationError)
        {
            // Status command doesn't require a payload
            validationError = string.Empty;
            return true;
        }

        private static bool ValidateRunTestsCommand(PipeCommand command, out string validationError)
        {
            if (string.IsNullOrWhiteSpace(command.Payload))
            {
                validationError = "RunTests command requires a test execution payload";
                return false;
            }

            try
            {
                var payload = JsonSerializer.Deserialize<RevitTestExecutor.TestExecutionPayload>(command.Payload);
                if (payload == null)
                {
                    validationError = "Invalid test execution payload format";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(payload.AssemblyPath))
                {
                    validationError = "Test execution payload must specify an assembly path";
                    return false;
                }

                if (payload.Tests == null || payload.Tests.Count == 0)
                {
                    validationError = "Test execution payload must contain at least one test";
                    return false;
                }
            }
            catch (JsonException)
            {
                validationError = "Test execution payload is not valid JSON";
                return false;
            }

            validationError = string.Empty;
            return true;
        }

        private static bool ValidateTestInitCommand(PipeCommand command, out string validationError)
        {
            // TestInit command doesn't require a payload
            validationError = string.Empty;
            return true;
        }

        private static bool CreateUnknownCommandError(string commandName, out string validationError)
        {
            validationError = $"Unknown command: {commandName}";
            return false;
        }
    }

    /// <summary>
    /// Constants for pipe command names.
    /// </summary>
    public static class PipeCommands
    {
        /// <summary>
        /// Command to reload the runtime from a specified path.
        /// </summary>
        public const string Reload = "RELOAD";

        /// <summary>
        /// Command to get the current runtime status.
        /// </summary>
        public const string Status = "STATUS";

        /// <summary>
        /// Command to run tests in the Revit context.
        /// </summary>
        public const string RunTests = "RUN_TESTS";

        /// <summary>
        /// Command to initialize the test execution environment.
        /// </summary>
        public const string TestInit = "TEST_INIT";
    }
}