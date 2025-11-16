namespace Rca.Tools.MetricsReporter.Services;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Rca.Tools.MetricsReporter.Logging;

/// <summary>
/// Manages baseline file replacement by comparing reports and archiving old baselines.
/// </summary>
public sealed class BaselineManager
{
    /// <summary>
    /// Compares two JSON files using hash comparison to determine if they differ.
    /// </summary>
    /// <param name="reportPath">Path to the new metrics report JSON file.</param>
    /// <param name="baselinePath">Path to the existing baseline JSON file.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// <see langword="true"/> if the files differ or if baseline doesn't exist; <see langword="false"/> if files are identical.
    /// </returns>
    /// <remarks>
    /// This method uses SHA256 hash comparison for fast and reliable file comparison.
    /// If the baseline file doesn't exist, the method returns <see langword="true"/> to indicate replacement is needed.
    /// </remarks>
    public async Task<bool> AreFilesDifferentAsync(string reportPath, string? baselinePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        if (!File.Exists(reportPath))
        {
            throw new FileNotFoundException($"Report file not found: {reportPath}", reportPath);
        }

        if (string.IsNullOrWhiteSpace(baselinePath) || !File.Exists(baselinePath))
        {
            // Baseline doesn't exist, so files are considered different
            return true;
        }

        var reportHash = await ComputeFileHashAsync(reportPath, cancellationToken).ConfigureAwait(false);
        var baselineHash = await ComputeFileHashAsync(baselinePath, cancellationToken).ConfigureAwait(false);

        return !reportHash.Equals(baselineHash, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Replaces the baseline file by archiving the old baseline and moving the new report to baseline location.
    /// </summary>
    /// <param name="reportPath">Path to the new metrics report JSON file that will become the baseline.</param>
    /// <param name="baselinePath">Path to the existing baseline JSON file that will be replaced.</param>
    /// <param name="storagePath">Directory path where the old baseline will be archived with a timestamp.</param>
    /// <param name="logger">Logger instance for recording operations.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// <see langword="true"/> if baseline was replaced; <see langword="false"/> if replacement was not needed or failed.
    /// </returns>
    /// <remarks>
    /// This method performs the following steps:
    /// 1. If old baseline exists, it is moved to storage directory with a timestamp suffix for unique filename.
    /// 2. The new report file is copied (not moved) to the baseline location to preserve the original report.
    /// 3. All operations are logged for traceability.
    /// </remarks>
    public async Task<bool> ReplaceBaselineAsync(
        string reportPath,
        string baselinePath,
        string? storagePath,
        FileLogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(baselinePath);
        ArgumentNullException.ThrowIfNull(logger);

        if (!File.Exists(reportPath))
        {
            logger.LogError($"Report file not found for baseline replacement: {reportPath}");
            return false;
        }

        try
        {
            // Archive old baseline if it exists
            if (File.Exists(baselinePath))
            {
                await ArchiveOldBaselineAsync(baselinePath, storagePath, logger, cancellationToken).ConfigureAwait(false);
            }

            // Ensure baseline directory exists
            var baselineDir = Path.GetDirectoryName(baselinePath);
            if (!string.IsNullOrWhiteSpace(baselineDir) && !Directory.Exists(baselineDir))
            {
                Directory.CreateDirectory(baselineDir);
                logger.LogInformation($"Created baseline directory: {baselineDir}");
            }

            // Copy new report to baseline location (copy to preserve original report)
            await CopyFileAsync(reportPath, baselinePath, cancellationToken).ConfigureAwait(false);
            logger.LogInformation($"Baseline replaced: {baselinePath} <- {reportPath}");

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError($"Failed to replace baseline: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Computes SHA256 hash of a file for fast comparison.
    /// </summary>
    /// <param name="filePath">Path to the file to hash.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>Hexadecimal string representation of the file hash.</returns>
    private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();

        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Archives the old baseline file to storage directory with a timestamp suffix.
    /// </summary>
    /// <param name="baselinePath">Path to the old baseline file.</param>
    /// <param name="storagePath">Directory where the archived baseline will be stored.</param>
    /// <param name="logger">Logger instance for recording operations.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <remarks>
    /// The archived file name format: metrics-baseline-YYYYMMDD-HHMMSS.json
    /// Uses local time (not UTC) as specified in requirements.
    /// </remarks>
    private static Task ArchiveOldBaselineAsync(
        string baselinePath,
        string? storagePath,
        FileLogger logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            logger.LogInformation("Storage path not specified, skipping baseline archive.");
            return Task.CompletedTask;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // Ensure storage directory exists
            if (!Directory.Exists(storagePath))
            {
                Directory.CreateDirectory(storagePath);
                logger.LogInformation($"Created storage directory: {storagePath}");
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Generate timestamp using local time (not UTC)
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            var baselineFileName = Path.GetFileName(baselinePath);
            var baselineNameWithoutExt = Path.GetFileNameWithoutExtension(baselineFileName);
            var baselineExt = Path.GetExtension(baselineFileName);

            var archivedFileName = $"{baselineNameWithoutExt}-{timestamp}{baselineExt}";
            var archivedPath = Path.Combine(storagePath, archivedFileName);

            // Move (not copy) the old baseline to archive location
            File.Move(baselinePath, archivedPath);
            logger.LogInformation($"Old baseline archived: {baselinePath} -> {archivedPath}");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            logger.LogError($"Failed to archive old baseline: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// Copies a file from source to destination asynchronously.
    /// </summary>
    /// <param name="sourcePath">Path to the source file.</param>
    /// <param name="destinationPath">Path to the destination file.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    private static async Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        await using var sourceStream = File.OpenRead(sourcePath);
        await using var destinationStream = File.Create(destinationPath);

        await sourceStream.CopyToAsync(destinationStream, cancellationToken).ConfigureAwait(false);
    }
}

