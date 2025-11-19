using NUnit.Framework;
using Rca.Loader.Services;

namespace Rca.Loader.Tests
{
  /// <summary>
  /// Tests for <see cref="PipeCommand"/> and <see cref="PipeResponse"/> record types.
  /// </summary>
  [TestFixture]
  public class PipeDataTypesTests
  {
    /// <summary>
    /// Verifies that PipeCommand can be instantiated with command and payload.
    /// </summary>
    [Test]
    public void PipeCommand_CanBeCreated_WithCommandAndPayload()
    {
      var command = new PipeCommand("TEST_CMD", "test payload");

      Assert.That(command.Command, Is.EqualTo("TEST_CMD"));
      Assert.That(command.Payload, Is.EqualTo("test payload"));
    }

    /// <summary>
    /// Verifies that PipeCommand can be instantiated with null payload.
    /// </summary>
    [Test]
    public void PipeCommand_CanBeCreated_WithNullPayload()
    {
      var command = new PipeCommand("TEST_CMD", null);

      Assert.That(command.Command, Is.EqualTo("TEST_CMD"));
      Assert.That(command.Payload, Is.Null);
    }

    /// <summary>
    /// Verifies that PipeCommand supports record equality.
    /// </summary>
    [Test]
    public void PipeCommand_SupportsRecordEquality()
    {
      var command1 = new PipeCommand("CMD", "payload");
      var command2 = new PipeCommand("CMD", "payload");
      var command3 = new PipeCommand("OTHER", "payload");

      Assert.That(command1, Is.EqualTo(command2));
      Assert.That(command1, Is.Not.EqualTo(command3));
    }

    /// <summary>
    /// Verifies that PipeResponse can be instantiated.
    /// </summary>
    [Test]
    public void PipeResponse_CanBeCreated()
    {
      var response = new PipeResponse
      {
        Status = "OK",
        Message = "Success"
      };

      Assert.That(response.Status, Is.EqualTo("OK"));
      Assert.That(response.Message, Is.EqualTo("Success"));
    }

    /// <summary>
    /// Verifies that PipeResponse has default empty values.
    /// </summary>
    [Test]
    public void PipeResponse_DefaultValues_ShouldBeEmpty()
    {
      var response = new PipeResponse();

      Assert.That(response.Status, Is.Empty);
      Assert.That(response.Message, Is.Empty);
    }

    /// <summary>
    /// Verifies that PipeResponse supports record equality.
    /// </summary>
    [Test]
    public void PipeResponse_SupportsRecordEquality()
    {
      var response1 = new PipeResponse { Status = "OK", Message = "Done" };
      var response2 = new PipeResponse { Status = "OK", Message = "Done" };
      var response3 = new PipeResponse { Status = "ERROR", Message = "Done" };

      Assert.That(response1, Is.EqualTo(response2));
      Assert.That(response1, Is.Not.EqualTo(response3));
    }

    /// <summary>
    /// Verifies that PipeResponse properties can be modified.
    /// </summary>
    [Test]
    public void PipeResponse_PropertiesCanBeModified()
    {
      var response = new PipeResponse();

      response.Status = "OK";
      response.Message = "Updated";

      Assert.That(response.Status, Is.EqualTo("OK"));
      Assert.That(response.Message, Is.EqualTo("Updated"));
    }
  }
}

