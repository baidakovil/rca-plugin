namespace Rca.Tools.MetricsReporter.MetricsReader.Services;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Rca.Tools.MetricsReporter.Configuration;
using Rca.Tools.MetricsReporter.Model;
using Rca.Tools.MetricsReporter.MetricsReader.Settings;
using Rca.Tools.MetricsReporter.Serialization;
using Rca.Tools.MetricsReporter.Services;

/// <summary>
/// Creates <see cref="MetricsReaderContext"/> instances based on CLI settings.
/// </summary>
internal static class MetricsReaderContextFactory
{
  public static async Task<MetricsReaderContext> CreateAsync(MetricsReaderSettingsBase settings, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(settings);

    var shouldUpdate = !settings.NoUpdate;
    var reportPath = ResolveReportPath(settings.ReportPath, allowMissing: shouldUpdate);
    if (shouldUpdate)
    {
      var solutionPath = SolutionLocator.FindSolutionPath(reportPath);
      var updater = new MetricsUpdater(solutionPath);
      await updater.UpdateAsync(cancellationToken).ConfigureAwait(false);
    }

    EnsureReportExists(reportPath);
    var report = await JsonReportLoader.LoadAsync(reportPath, cancellationToken).ConfigureAwait(false)
                 ?? throw new InvalidOperationException($"Failed to load metrics report: {reportPath}");

    var overrideThresholds = await LoadThresholdOverridesAsync(settings.ThresholdsFile, cancellationToken).ConfigureAwait(false);
    var thresholdsByLevel = new ReadOnlyDictionary<MetricIdentifier, IDictionary<MetricSymbolLevel, MetricThreshold>>(report.Metadata.ThresholdsByLevel);
    var thresholdProvider = new MetricsThresholdProvider(thresholdsByLevel, overrideThresholds);
    var suppressedIndex = SuppressedSymbolIndex.Create(report.Metadata.SuppressedSymbols);

    return new MetricsReaderContext(report, thresholdProvider, suppressedIndex, settings.IncludeSuppressed);
  }

  private static string ResolveReportPath(string? path, bool allowMissing)
  {
    var resolved = string.IsNullOrWhiteSpace(path) ? string.Empty : path;
    resolved = Path.GetFullPath(resolved);
    if (!allowMissing && !File.Exists(resolved))
    {
      throw new FileNotFoundException($"Metrics report not found: {resolved}", resolved);
    }

    return resolved;
  }

  private static void EnsureReportExists(string path)
  {
    if (!File.Exists(path))
    {
      throw new FileNotFoundException($"Metrics report not found: {path}", path);
    }
  }

  private static async Task<IReadOnlyDictionary<MetricIdentifier, MetricThresholdDefinition>?> LoadThresholdOverridesAsync(
    string? thresholdsPath,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(thresholdsPath))
    {
      return null;
    }

    var absolutePath = Path.GetFullPath(thresholdsPath);
    if (!File.Exists(absolutePath))
    {
      throw new FileNotFoundException($"Thresholds override file not found: {absolutePath}", absolutePath);
    }

    await using var stream = File.OpenRead(absolutePath);
    var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    var payload = document.RootElement.GetRawText();
    return ThresholdsParser.Parse(payload);
  }
}

