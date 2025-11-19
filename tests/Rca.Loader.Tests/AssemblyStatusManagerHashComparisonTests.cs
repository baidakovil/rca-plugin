using NUnit.Framework;
using FluentAssertions;
using Rca.Loader.AssemblyManagement;
using Rca.Loader.Infrastructure;

namespace Rca.Loader.Tests
{
  /// <summary>
  /// Unit tests for <see cref="AssemblyStatusManager.CompareHashes"/>.
  /// </summary>
  /// <remarks>
  /// Exercises the centralized hash comparison logic with a variety of
  /// installed/latest hash combinations to ensure case-insensitive equality
  /// and proper handling of missing or marker values.
  /// </remarks>
  [TestFixture]
  public class AssemblyStatusManagerHashComparisonTests
  {
    /// <summary>
    /// Verifies that <see cref="AssemblyStatusManager.CompareHashes(string?, string?)"/>
    /// returns the expected boolean result for the provided inputs.
    /// </summary>
    /// <param name="installedHash">The currently installed assembly hash. May be <see langword="null"/> or empty.</param>
    /// <param name="latestHash">The latest discovered assembly hash.</param>
    /// <param name="expected">Expected comparison result: <see langword="true"/> when hashes differ.</param>
    [Test]
    [TestCase("abc123", "abc123", false)]
    [TestCase("ABC123", "abc123", false)]
    [TestCase("abc123", "def456", true)]
    [TestCase(null, "def456", false)]
    [TestCase("", "def456", false)]
    [TestCase(AttributeMetadataLoader.MissingMarker, "def456", false)]
    public void CompareHashes_VariousCombinations_ReturnsExpected(
        string installedHash,
        string latestHash,
        bool expected)
    {
      // Act
      var result = AssemblyStatusManager.CompareHashes(installedHash, latestHash);

      // Assert
      result.Should().Be(expected);
    }
  }
}
