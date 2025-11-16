namespace Rca.MetricsReporter.Tests.Services;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Rca.Tools.MetricsReporter.Logging;
using Rca.Tools.MetricsReporter.Services;

/// <summary>
/// Unit tests for <see cref="BaselineManager"/> class.
/// </summary>
[TestFixture]
[Category("Unit")]
public sealed class BaselineManagerTests
{
    private BaselineManager manager = null!;
    private string? testDirectory;
    private string? logFilePath;

    [SetUp]
    public void SetUp()
    {
        manager = new BaselineManager();
        testDirectory = Path.Combine(Path.GetTempPath(), "RCA_BaselineManagerTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDirectory);
        logFilePath = Path.Combine(testDirectory, "test.log");
    }

    [TearDown]
    public void TearDown()
    {
        if (testDirectory != null && Directory.Exists(testDirectory))
        {
            try
            {
                Directory.Delete(testDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Test]
    public async Task AreFilesDifferentAsync_WhenFilesAreIdentical_ReturnsFalse()
    {
        // Arrange
        var reportPath = Path.Combine(testDirectory!, "report.json");
        var baselinePath = Path.Combine(testDirectory!, "baseline.json");
        const string content = """{"test": "data"}""";

        await File.WriteAllTextAsync(reportPath, content);
        await File.WriteAllTextAsync(baselinePath, content);

        // Act
        var result = await manager.AreFilesDifferentAsync(reportPath, baselinePath, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task AreFilesDifferentAsync_WhenFilesDiffer_ReturnsTrue()
    {
        // Arrange
        var reportPath = Path.Combine(testDirectory!, "report.json");
        var baselinePath = Path.Combine(testDirectory!, "baseline.json");

        await File.WriteAllTextAsync(reportPath, """{"test": "data1"}""");
        await File.WriteAllTextAsync(baselinePath, """{"test": "data2"}""");

        // Act
        var result = await manager.AreFilesDifferentAsync(reportPath, baselinePath, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task AreFilesDifferentAsync_WhenBaselineDoesNotExist_ReturnsTrue()
    {
        // Arrange
        var reportPath = Path.Combine(testDirectory!, "report.json");
        var baselinePath = Path.Combine(testDirectory!, "nonexistent.json");

        await File.WriteAllTextAsync(reportPath, """{"test": "data"}""");

        // Act
        var result = await manager.AreFilesDifferentAsync(reportPath, baselinePath, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task AreFilesDifferentAsync_WhenBaselinePathIsNull_ReturnsTrue()
    {
        // Arrange
        var reportPath = Path.Combine(testDirectory!, "report.json");

        await File.WriteAllTextAsync(reportPath, """{"test": "data"}""");

        // Act
        var result = await manager.AreFilesDifferentAsync(reportPath, null, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task AreFilesDifferentAsync_WhenBaselinePathIsEmpty_ReturnsTrue()
    {
        // Arrange
        var reportPath = Path.Combine(testDirectory!, "report.json");

        await File.WriteAllTextAsync(reportPath, """{"test": "data"}""");

        // Act
        var result = await manager.AreFilesDifferentAsync(reportPath, string.Empty, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task AreFilesDifferentAsync_WhenReportDoesNotExist_ThrowsFileNotFoundException()
    {
        // Arrange
        var reportPath = Path.Combine(testDirectory!, "nonexistent.json");
        var baselinePath = Path.Combine(testDirectory!, "baseline.json");

        // Act & Assert
        var act = async () => await manager.AreFilesDifferentAsync(reportPath, baselinePath, CancellationToken.None).ConfigureAwait(false);
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Test]
    public async Task AreFilesDifferentAsync_WhenFilesDifferByWhitespace_ReturnsTrue()
    {
        // Arrange
        var reportPath = Path.Combine(testDirectory!, "report.json");
        var baselinePath = Path.Combine(testDirectory!, "baseline.json");

        await File.WriteAllTextAsync(reportPath, """{"test":"data"}""");
        await File.WriteAllTextAsync(baselinePath, """{"test": "data"}""");

        // Act
        var result = await manager.AreFilesDifferentAsync(reportPath, baselinePath, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task ReplaceBaselineAsync_WhenBaselineExists_ArchivesOldBaselineAndReplacesIt()
    {
        // Arrange
        var reportPath = Path.Combine(testDirectory!, "report.json");
        var baselinePath = Path.Combine(testDirectory!, "baseline.json");
        var storagePath = Path.Combine(testDirectory!, "storage");

        const string reportContent = """{"new": "data"}""";
        const string oldBaselineContent = """{"old": "data"}""";

        await File.WriteAllTextAsync(reportPath, reportContent);
        await File.WriteAllTextAsync(baselinePath, oldBaselineContent);

        using var logger = new FileLogger(logFilePath!);

        // Act
        var result = await manager.ReplaceBaselineAsync(reportPath, baselinePath, storagePath, logger, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeTrue();
        File.Exists(baselinePath).Should().BeTrue("baseline should exist after replacement");
        var newBaselineContent = await File.ReadAllTextAsync(baselinePath);
        newBaselineContent.Should().Be(reportContent, "baseline should contain report content");

        // Verify old baseline was archived
        var archivedFiles = Directory.GetFiles(storagePath, "baseline-*.json");
        archivedFiles.Should().HaveCount(1, "old baseline should be archived");
        
        var archivedContent = await File.ReadAllTextAsync(archivedFiles[0]);
        archivedContent.Should().Be(oldBaselineContent, "archived baseline should contain old content");
    }

    [Test]
    public async Task ReplaceBaselineAsync_WhenBaselineDoesNotExist_CreatesNewBaseline()
    {
        // Arrange
        var reportPath = Path.Combine(testDirectory!, "report.json");
        var baselinePath = Path.Combine(testDirectory!, "baseline.json");
        var storagePath = Path.Combine(testDirectory!, "storage");

        const string reportContent = """{"new": "data"}""";

        await File.WriteAllTextAsync(reportPath, reportContent);

        using var logger = new FileLogger(logFilePath!);

        // Act
        var result = await manager.ReplaceBaselineAsync(reportPath, baselinePath, storagePath, logger, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeTrue();
        File.Exists(baselinePath).Should().BeTrue("baseline should be created");
        var newBaselineContent = await File.ReadAllTextAsync(baselinePath);
        newBaselineContent.Should().Be(reportContent, "baseline should contain report content");

        // Verify no archived files were created
        if (Directory.Exists(storagePath))
        {
            var archivedFiles = Directory.GetFiles(storagePath, "baseline-*.json");
            archivedFiles.Should().BeEmpty("no baseline to archive");
        }
    }

    [Test]
    public async Task ReplaceBaselineAsync_WhenStoragePathIsNull_SkipsArchive()
    {
        // Arrange
        var reportPath = Path.Combine(testDirectory!, "report.json");
        var baselinePath = Path.Combine(testDirectory!, "baseline.json");

        const string reportContent = """{"new": "data"}""";
        const string oldBaselineContent = """{"old": "data"}""";

        await File.WriteAllTextAsync(reportPath, reportContent);
        await File.WriteAllTextAsync(baselinePath, oldBaselineContent);

        using var logger = new FileLogger(logFilePath!);

        // Act
        var result = await manager.ReplaceBaselineAsync(reportPath, baselinePath, null, logger, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeTrue();
        File.Exists(baselinePath).Should().BeTrue("baseline should exist after replacement");
        var newBaselineContent = await File.ReadAllTextAsync(baselinePath);
        newBaselineContent.Should().Be(reportContent, "baseline should contain report content");
    }

    [Test]
    public async Task ReplaceBaselineAsync_WhenStoragePathIsEmpty_SkipsArchive()
    {
        // Arrange
        var reportPath = Path.Combine(testDirectory!, "report.json");
        var baselinePath = Path.Combine(testDirectory!, "baseline.json");

        const string reportContent = """{"new": "data"}""";
        const string oldBaselineContent = """{"old": "data"}""";

        await File.WriteAllTextAsync(reportPath, reportContent);
        await File.WriteAllTextAsync(baselinePath, oldBaselineContent);

        using var logger = new FileLogger(logFilePath!);

        // Act
        var result = await manager.ReplaceBaselineAsync(reportPath, baselinePath, string.Empty, logger, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeTrue();
        File.Exists(baselinePath).Should().BeTrue("baseline should exist after replacement");
    }

    [Test]
    public async Task ReplaceBaselineAsync_ArchivedBaselineHasTimestampSuffix()
    {
        // Arrange
        var reportPath = Path.Combine(testDirectory!, "report.json");
        var baselinePath = Path.Combine(testDirectory!, "baseline.json");
        var storagePath = Path.Combine(testDirectory!, "storage");

        await File.WriteAllTextAsync(reportPath, """{"new": "data"}""");
        await File.WriteAllTextAsync(baselinePath, """{"old": "data"}""");

        using var logger = new FileLogger(logFilePath!);

        // Act
        await manager.ReplaceBaselineAsync(reportPath, baselinePath, storagePath, logger, CancellationToken.None).ConfigureAwait(false);

        // Assert
        var archivedFiles = Directory.GetFiles(storagePath, "baseline-*.json");
        archivedFiles.Should().HaveCount(1);
        
        var archivedFileName = Path.GetFileName(archivedFiles[0]);
        archivedFileName.Should().MatchRegex(@"^baseline-\d{8}-\d{6}\.json$", "archived file should have timestamp format YYYYMMDD-HHMMSS");
    }

    [Test]
    public async Task ReplaceBaselineAsync_WhenStorageDirectoryDoesNotExist_CreatesIt()
    {
        // Arrange
        var reportPath = Path.Combine(testDirectory!, "report.json");
        var baselinePath = Path.Combine(testDirectory!, "baseline.json");
        var storagePath = Path.Combine(testDirectory!, "storage", "subdirectory");

        await File.WriteAllTextAsync(reportPath, """{"new": "data"}""");
        await File.WriteAllTextAsync(baselinePath, """{"old": "data"}""");

        using var logger = new FileLogger(logFilePath!);

        // Act
        var result = await manager.ReplaceBaselineAsync(reportPath, baselinePath, storagePath, logger, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeTrue();
        Directory.Exists(storagePath).Should().BeTrue("storage directory should be created");
    }

    [Test]
    public async Task ReplaceBaselineAsync_WhenBaselineDirectoryDoesNotExist_CreatesIt()
    {
        // Arrange
        var reportPath = Path.Combine(testDirectory!, "report.json");
        var baselinePath = Path.Combine(testDirectory!, "subdir", "baseline.json");
        var storagePath = Path.Combine(testDirectory!, "storage");

        await File.WriteAllTextAsync(reportPath, """{"new": "data"}""");

        using var logger = new FileLogger(logFilePath!);

        // Act
        var result = await manager.ReplaceBaselineAsync(reportPath, baselinePath, storagePath, logger, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeTrue();
        File.Exists(baselinePath).Should().BeTrue("baseline should be created in subdirectory");
    }

    [Test]
    public async Task ReplaceBaselineAsync_WhenReportDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var reportPath = Path.Combine(testDirectory!, "nonexistent.json");
        var baselinePath = Path.Combine(testDirectory!, "baseline.json");
        var storagePath = Path.Combine(testDirectory!, "storage");

        using var logger = new FileLogger(logFilePath!);

        // Act
        var result = await manager.ReplaceBaselineAsync(reportPath, baselinePath, storagePath, logger, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeFalse("should return false when report file does not exist");
    }

    [Test]
    public async Task ReplaceBaselineAsync_PreservesOriginalReportFile()
    {
        // Arrange
        var reportPath = Path.Combine(testDirectory!, "report.json");
        var baselinePath = Path.Combine(testDirectory!, "baseline.json");
        var storagePath = Path.Combine(testDirectory!, "storage");

        const string reportContent = """{"new": "data"}""";

        await File.WriteAllTextAsync(reportPath, reportContent);

        using var logger = new FileLogger(logFilePath!);

        // Act
        await manager.ReplaceBaselineAsync(reportPath, baselinePath, storagePath, logger, CancellationToken.None).ConfigureAwait(false);

        // Assert
        File.Exists(reportPath).Should().BeTrue("original report file should still exist");
        var originalContent = await File.ReadAllTextAsync(reportPath);
        originalContent.Should().Be(reportContent, "original report content should be preserved");
    }

    [Test]
    public async Task ReplaceBaselineAsync_MultipleArchivesHaveUniqueNames()
    {
        // Arrange
        var reportPath1 = Path.Combine(testDirectory!, "report1.json");
        var reportPath2 = Path.Combine(testDirectory!, "report2.json");
        var baselinePath = Path.Combine(testDirectory!, "baseline.json");
        var storagePath = Path.Combine(testDirectory!, "storage");

        await File.WriteAllTextAsync(reportPath1, """{"v1": "data"}""");
        await File.WriteAllTextAsync(baselinePath, """{"v0": "data"}""");

        using var logger = new FileLogger(logFilePath!);

        // Act - Replace baseline twice with a small delay to ensure different timestamps
        await manager.ReplaceBaselineAsync(reportPath1, baselinePath, storagePath, logger, CancellationToken.None).ConfigureAwait(false);
        await Task.Delay(1100); // Wait more than 1 second to ensure timestamp difference
        await File.WriteAllTextAsync(reportPath2, """{"v2": "data"}""");
        await manager.ReplaceBaselineAsync(reportPath2, baselinePath, storagePath, logger, CancellationToken.None).ConfigureAwait(false);

        // Assert
        var archivedFiles = Directory.GetFiles(storagePath, "baseline-*.json");
        archivedFiles.Should().HaveCount(2, "both old baselines should be archived");
        
        var fileNames = Array.ConvertAll(archivedFiles, Path.GetFileName);
        fileNames.Should().OnlyHaveUniqueItems("all archived files should have unique names");
    }

    [Test]
    public async Task AreFilesDifferentAsync_WhenCancelled_ThrowsOperationCancelledException()
    {
        // Arrange
        var reportPath = Path.Combine(testDirectory!, "report.json");
        var baselinePath = Path.Combine(testDirectory!, "baseline.json");

        await File.WriteAllTextAsync(reportPath, """{"test": "data"}""");
        await File.WriteAllTextAsync(baselinePath, """{"test": "data"}""");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = async () => await manager.AreFilesDifferentAsync(reportPath, baselinePath, cts.Token).ConfigureAwait(false);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

}

