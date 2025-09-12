#nullable enable
using Rca.Loader.Services;

namespace Rca.Loader.Infrastructure
{
    /// <summary>
    /// Factory for creating standardized pipe responses.
    /// </summary>
    public static class PipeResponseFactory
    {
        /// <summary>
        /// Creates a successful response.
        /// </summary>
        /// <param name="message">Optional success message.</param>
        /// <returns>A success response.</returns>
        public static PipeResponse Success(string message = "")
        {
            return new PipeResponse 
            { 
                Status = PipeResponseStatus.Success, 
                Message = message 
            };
        }

        /// <summary>
        /// Creates an error response.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <returns>An error response.</returns>
        public static PipeResponse Error(string message)
        {
            return new PipeResponse 
            { 
                Status = PipeResponseStatus.Error, 
                Message = message ?? string.Empty 
            };
        }

        /// <summary>
        /// Creates a response indicating the runtime is loaded.
        /// </summary>
        /// <param name="runtimePath">The path to the loaded runtime.</param>
        /// <returns>A loaded status response.</returns>
        public static PipeResponse Loaded(string runtimePath)
        {
            return new PipeResponse 
            { 
                Status = PipeResponseStatus.Loaded, 
                Message = runtimePath ?? string.Empty 
            };
        }

        /// <summary>
        /// Creates a response indicating no runtime is loaded.
        /// </summary>
        /// <returns>An empty status response.</returns>
        public static PipeResponse Empty()
        {
            return new PipeResponse 
            { 
                Status = PipeResponseStatus.Empty, 
                Message = string.Empty 
            };
        }

        /// <summary>
        /// Creates a response for unknown commands.
        /// </summary>
        /// <param name="command">The unknown command that was attempted.</param>
        /// <returns>An error response for unknown commands.</returns>
        public static PipeResponse UnknownCommand(string command)
        {
            return Error($"Unknown command: {command}");
        }

        /// <summary>
        /// Creates a response for invalid payloads.
        /// </summary>
        /// <param name="reason">The reason the payload is invalid.</param>
        /// <returns>An error response for invalid payloads.</returns>
        public static PipeResponse InvalidPayload(string reason)
        {
            return Error($"Invalid payload: {reason}");
        }
    }

    /// <summary>
    /// Standard response status values for pipe responses.
    /// </summary>
    public static class PipeResponseStatus
    {
        /// <summary>
        /// Indicates the operation completed successfully.
        /// </summary>
        public const string Success = "OK";

        /// <summary>
        /// Indicates an error occurred during the operation.
        /// </summary>
        public const string Error = "ERROR";

        /// <summary>
        /// Indicates the runtime is currently loaded.
        /// </summary>
        public const string Loaded = "LOADED";

        /// <summary>
        /// Indicates no runtime is currently loaded.
        /// </summary>
        public const string Empty = "EMPTY";
    }
}