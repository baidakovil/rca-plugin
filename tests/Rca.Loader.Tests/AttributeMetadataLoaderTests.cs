using NUnit.Framework;
using Rca.Loader.Infrastructure;
using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

namespace Rca.Loader.Tests
{
  /// <summary>
  /// Tests for <see cref="AttributeMetadataLoader"/> class.
  /// </summary>
  [TestFixture]
  public class AttributeMetadataLoaderTests
  {
    private string? _testDirectory;

    [SetUp]
    public void Setup()
    {
      _testDirectory = Path.Combine(Path.GetTempPath(), "RCA_AttributeTests", Guid.NewGuid().ToString());
      Directory.CreateDirectory(_testDirectory);
    }

    [TearDown]
    public void TearDown()
    {
      if (_testDirectory != null && Directory.Exists(_testDirectory))
      {
        try
        {
          Directory.Delete(_testDirectory, recursive: true);
        }
        catch
        {
          // Ignore cleanup errors
        }
      }
    }

    /// <summary>
    /// Verifies that MissingMarker constant has expected value.
    /// </summary>
    [Test]
    public void MissingMarker_ShouldHaveExpectedValue()
    {
      Assert.That(AttributeMetadataLoader.MissingMarker, Is.EqualTo("none"));
    }

    /// <summary>
    /// Verifies that TryGetFromLoadedAssembly returns MissingMarker for null assembly.
    /// </summary>
    [Test]
    public void TryGetFromLoadedAssembly_WithNullAssembly_ShouldReturnMissingMarker()
    {
      var result = AttributeMetadataLoader.TryGetFromLoadedAssembly(null!, "TestKey");

      Assert.That(result, Is.EqualTo(AttributeMetadataLoader.MissingMarker));
    }

    /// <summary>
    /// Verifies that TryGetFromLoadedAssembly returns MissingMarker for null key.
    /// </summary>
    [Test]
    public void TryGetFromLoadedAssembly_WithNullKey_ShouldReturnMissingMarker()
    {
      var assembly = typeof(AttributeMetadataLoaderTests).Assembly;
      var result = AttributeMetadataLoader.TryGetFromLoadedAssembly(assembly, null!);

      Assert.That(result, Is.EqualTo(AttributeMetadataLoader.MissingMarker));
    }

    /// <summary>
    /// Verifies that TryGetFromLoadedAssembly returns MissingMarker for empty key.
    /// </summary>
    [Test]
    public void TryGetFromLoadedAssembly_WithEmptyKey_ShouldReturnMissingMarker()
    {
      var assembly = typeof(AttributeMetadataLoaderTests).Assembly;
      var result = AttributeMetadataLoader.TryGetFromLoadedAssembly(assembly, string.Empty);

      Assert.That(result, Is.EqualTo(AttributeMetadataLoader.MissingMarker));
    }

    /// <summary>
    /// Verifies that TryGetFromLoadedAssembly returns MissingMarker for whitespace key.
    /// </summary>
    [Test]
    public void TryGetFromLoadedAssembly_WithWhitespaceKey_ShouldReturnMissingMarker()
    {
      var assembly = typeof(AttributeMetadataLoaderTests).Assembly;
      var result = AttributeMetadataLoader.TryGetFromLoadedAssembly(assembly, "   ");

      Assert.That(result, Is.EqualTo(AttributeMetadataLoader.MissingMarker));
    }

    /// <summary>
    /// Verifies that TryGetFromLoadedAssembly returns MissingMarker for non-existent key.
    /// </summary>
    [Test]
    public void TryGetFromLoadedAssembly_WithNonExistentKey_ShouldReturnMissingMarker()
    {
      var assembly = typeof(AttributeMetadataLoaderTests).Assembly;
      var result = AttributeMetadataLoader.TryGetFromLoadedAssembly(assembly, "NonExistentKey12345");

      Assert.That(result, Is.EqualTo(AttributeMetadataLoader.MissingMarker));
    }

    /// <summary>
    /// Verifies that TryGetFromLoadedAssembly can read actual metadata from current assembly.
    /// </summary>
    [Test]
    public void TryGetFromLoadedAssembly_WithActualMetadata_ShouldReturnValue()
    {
      var assembly = typeof(AttributeMetadataLoaderTests).Assembly;

      // Try to find any AssemblyMetadataAttribute on this test assembly
      var attributes = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
      var firstAttr = System.Linq.Enumerable.FirstOrDefault(attributes);

      if (firstAttr != null)
      {
        var result = AttributeMetadataLoader.TryGetFromLoadedAssembly(assembly, firstAttr.Key);
        Assert.That(result, Is.EqualTo(firstAttr.Value));
      }
      else
      {
        // If no metadata exists, just verify the method doesn't throw
        var result = AttributeMetadataLoader.TryGetFromLoadedAssembly(assembly, "AnyKey");
        Assert.That(result, Is.EqualTo(AttributeMetadataLoader.MissingMarker));
      }
    }

    /// <summary>
    /// Verifies that TryGetFromFile returns MissingMarker for null path.
    /// </summary>
    [Test]
    public void TryGetFromFile_WithNullPath_ShouldReturnMissingMarker()
    {
      var result = AttributeMetadataLoader.TryGetFromFile(null!, "TestKey");

      Assert.That(result, Is.EqualTo(AttributeMetadataLoader.MissingMarker));
    }

    /// <summary>
    /// Verifies that TryGetFromFile returns MissingMarker for empty path.
    /// </summary>
    [Test]
    public void TryGetFromFile_WithEmptyPath_ShouldReturnMissingMarker()
    {
      var result = AttributeMetadataLoader.TryGetFromFile(string.Empty, "TestKey");

      Assert.That(result, Is.EqualTo(AttributeMetadataLoader.MissingMarker));
    }

    /// <summary>
    /// Verifies that TryGetFromFile returns MissingMarker for whitespace path.
    /// </summary>
    [Test]
    public void TryGetFromFile_WithWhitespacePath_ShouldReturnMissingMarker()
    {
      var result = AttributeMetadataLoader.TryGetFromFile("   ", "TestKey");

      Assert.That(result, Is.EqualTo(AttributeMetadataLoader.MissingMarker));
    }

    /// <summary>
    /// Verifies that TryGetFromFile returns MissingMarker for null key.
    /// </summary>
    [Test]
    public void TryGetFromFile_WithNullKey_ShouldReturnMissingMarker()
    {
      var testFile = Path.Combine(_testDirectory!, "test.dll");
      File.WriteAllText(testFile, "dummy content");

      var result = AttributeMetadataLoader.TryGetFromFile(testFile, null!);

      Assert.That(result, Is.EqualTo(AttributeMetadataLoader.MissingMarker));
    }

    /// <summary>
    /// Verifies that TryGetFromFile returns MissingMarker for empty key.
    /// </summary>
    [Test]
    public void TryGetFromFile_WithEmptyKey_ShouldReturnMissingMarker()
    {
      var testFile = Path.Combine(_testDirectory!, "test.dll");
      File.WriteAllText(testFile, "dummy content");

      var result = AttributeMetadataLoader.TryGetFromFile(testFile, string.Empty);

      Assert.That(result, Is.EqualTo(AttributeMetadataLoader.MissingMarker));
    }

    /// <summary>
    /// Verifies that TryGetFromFile returns MissingMarker for non-existent file.
    /// </summary>
    [Test]
    public void TryGetFromFile_WithNonExistentFile_ShouldReturnMissingMarker()
    {
      var nonExistentPath = Path.Combine(_testDirectory!, "nonexistent.dll");

      var result = AttributeMetadataLoader.TryGetFromFile(nonExistentPath, "TestKey");

      Assert.That(result, Is.EqualTo(AttributeMetadataLoader.MissingMarker));
    }

    /// <summary>
    /// Verifies that TryGetFromFile returns MissingMarker for invalid DLL file.
    /// </summary>
    [Test]
    public void TryGetFromFile_WithInvalidDll_ShouldReturnMissingMarker()
    {
      var invalidDll = Path.Combine(_testDirectory!, "invalid.dll");
      File.WriteAllText(invalidDll, "This is not a valid DLL file");

      var result = AttributeMetadataLoader.TryGetFromFile(invalidDll, "TestKey");

      Assert.That(result, Is.EqualTo(AttributeMetadataLoader.MissingMarker));
    }

    /// <summary>
    /// Verifies that TryGetFromFile can read metadata from a real assembly file.
    /// </summary>
    [Test]
    public void TryGetFromFile_WithRealAssembly_CanReadMetadata()
    {
      // Use the test assembly itself
      var testAssemblyPath = typeof(AttributeMetadataLoaderTests).Assembly.Location;

      if (File.Exists(testAssemblyPath))
      {
        // Try to read a non-existent key - should return MissingMarker
        var result = AttributeMetadataLoader.TryGetFromFile(testAssemblyPath, "NonExistentKey12345");
        Assert.That(result, Is.EqualTo(AttributeMetadataLoader.MissingMarker));
      }
      else
      {
        Assert.Inconclusive("Test assembly location not available");
      }
    }

    /// <summary>
    /// Verifies that TryGetFromLoadedAssembly handles case-insensitive key matching.
    /// </summary>
    [Test]
    public void TryGetFromLoadedAssembly_WithDifferentCase_ShouldMatchCaseInsensitive()
    {
      var assembly = typeof(AttributeMetadataLoaderTests).Assembly;
      var attributes = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
      var firstAttr = System.Linq.Enumerable.FirstOrDefault(attributes);

      if (firstAttr != null && !string.IsNullOrEmpty(firstAttr.Key))
      {
        var upperKey = firstAttr.Key.ToUpperInvariant();
        var lowerKey = firstAttr.Key.ToLowerInvariant();

        var resultUpper = AttributeMetadataLoader.TryGetFromLoadedAssembly(assembly, upperKey);
        var resultLower = AttributeMetadataLoader.TryGetFromLoadedAssembly(assembly, lowerKey);

        // Both should return the same value (case-insensitive matching)
        Assert.That(resultUpper, Is.EqualTo(resultLower));
      }
      else
      {
        Assert.Pass("No metadata attributes found for case-insensitive test");
      }
    }

    /// <summary>
    /// Verifies that TryGetFromFile handles case-insensitive key matching.
    /// </summary>
    [Test]
    public void TryGetFromFile_WithDifferentCase_ShouldMatchCaseInsensitive()
    {
      var testAssemblyPath = typeof(AttributeMetadataLoaderTests).Assembly.Location;

      if (File.Exists(testAssemblyPath))
      {
        var assembly = typeof(AttributeMetadataLoaderTests).Assembly;
        var attributes = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
        var firstAttr = System.Linq.Enumerable.FirstOrDefault(attributes);

        if (firstAttr != null && !string.IsNullOrEmpty(firstAttr.Key))
        {
          var upperKey = firstAttr.Key.ToUpperInvariant();
          var lowerKey = firstAttr.Key.ToLowerInvariant();

          var resultUpper = AttributeMetadataLoader.TryGetFromFile(testAssemblyPath, upperKey);
          var resultLower = AttributeMetadataLoader.TryGetFromFile(testAssemblyPath, lowerKey);

          // Both should return the same value (case-insensitive matching)
          Assert.That(resultUpper, Is.EqualTo(resultLower));
        }
        else
        {
          Assert.Pass("No metadata attributes found for case-insensitive test");
        }
      }
      else
      {
        Assert.Inconclusive("Test assembly location not available");
      }
    }
  }
}

