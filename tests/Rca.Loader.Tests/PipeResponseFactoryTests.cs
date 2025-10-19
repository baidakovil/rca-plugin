using NUnit.Framework;
using Rca.Loader.Infrastructure;
using Rca.Loader.Services;

namespace Rca.Loader.Tests
{
    /// <summary>
    /// Tests for <see cref="PipeResponseFactory"/> class.
    /// </summary>
    [TestFixture]
    public class PipeResponseFactoryTests
    {
        /// <summary>
        /// Verifies that Success creates response with OK status.
        /// </summary>
        [Test]
        public void Success_WithoutMessage_ShouldReturnOkStatus()
        {
            var response = PipeResponseFactory.Success();

            Assert.That(response.Status, Is.EqualTo(PipeResponseStatus.Success));
            Assert.That(response.Message, Is.Empty);
        }

        /// <summary>
        /// Verifies that Success creates response with specified message.
        /// </summary>
        [Test]
        public void Success_WithMessage_ShouldReturnOkStatusAndMessage()
        {
            var response = PipeResponseFactory.Success("Operation completed");

            Assert.That(response.Status, Is.EqualTo(PipeResponseStatus.Success));
            Assert.That(response.Message, Is.EqualTo("Operation completed"));
        }

        /// <summary>
        /// Verifies that Error creates response with ERROR status.
        /// </summary>
        [Test]
        public void Error_WithMessage_ShouldReturnErrorStatus()
        {
            var response = PipeResponseFactory.Error("Something went wrong");

            Assert.That(response.Status, Is.EqualTo(PipeResponseStatus.Error));
            Assert.That(response.Message, Is.EqualTo("Something went wrong"));
        }

        /// <summary>
        /// Verifies that Error handles null message gracefully.
        /// </summary>
        [Test]
        public void Error_WithNullMessage_ShouldReturnEmptyMessage()
        {
            var response = PipeResponseFactory.Error(null!);

            Assert.That(response.Status, Is.EqualTo(PipeResponseStatus.Error));
            Assert.That(response.Message, Is.Empty);
        }

        /// <summary>
        /// Verifies that Loaded creates response with LOADED status.
        /// </summary>
        [Test]
        public void Loaded_WithPath_ShouldReturnLoadedStatus()
        {
            var response = PipeResponseFactory.Loaded(@"C:\path\to\runtime.dll");

            Assert.That(response.Status, Is.EqualTo(PipeResponseStatus.Loaded));
            Assert.That(response.Message, Is.EqualTo(@"C:\path\to\runtime.dll"));
        }

        /// <summary>
        /// Verifies that Loaded handles null path gracefully.
        /// </summary>
        [Test]
        public void Loaded_WithNullPath_ShouldReturnEmptyMessage()
        {
            var response = PipeResponseFactory.Loaded(null!);

            Assert.That(response.Status, Is.EqualTo(PipeResponseStatus.Loaded));
            Assert.That(response.Message, Is.Empty);
        }

        /// <summary>
        /// Verifies that Empty creates response with EMPTY status.
        /// </summary>
        [Test]
        public void Empty_ShouldReturnEmptyStatus()
        {
            var response = PipeResponseFactory.Empty();

            Assert.That(response.Status, Is.EqualTo(PipeResponseStatus.Empty));
            Assert.That(response.Message, Is.Empty);
        }

        /// <summary>
        /// Verifies that UnknownCommand creates error response with command name.
        /// </summary>
        [Test]
        public void UnknownCommand_ShouldReturnErrorWithCommandName()
        {
            var response = PipeResponseFactory.UnknownCommand("INVALID_CMD");

            Assert.That(response.Status, Is.EqualTo(PipeResponseStatus.Error));
            Assert.That(response.Message, Does.Contain("Unknown command"));
            Assert.That(response.Message, Does.Contain("INVALID_CMD"));
        }

        /// <summary>
        /// Verifies that InvalidPayload creates error response with reason.
        /// </summary>
        [Test]
        public void InvalidPayload_ShouldReturnErrorWithReason()
        {
            var response = PipeResponseFactory.InvalidPayload("Missing required field");

            Assert.That(response.Status, Is.EqualTo(PipeResponseStatus.Error));
            Assert.That(response.Message, Does.Contain("Invalid payload"));
            Assert.That(response.Message, Does.Contain("Missing required field"));
        }

        /// <summary>
        /// Verifies PipeResponseStatus constants have expected values.
        /// </summary>
        [Test]
        public void PipeResponseStatus_ConstantsShouldHaveExpectedValues()
        {
            Assert.That(PipeResponseStatus.Success, Is.EqualTo("OK"));
            Assert.That(PipeResponseStatus.Error, Is.EqualTo("ERROR"));
            Assert.That(PipeResponseStatus.Loaded, Is.EqualTo("LOADED"));
            Assert.That(PipeResponseStatus.Empty, Is.EqualTo("EMPTY"));
        }
    }
}

