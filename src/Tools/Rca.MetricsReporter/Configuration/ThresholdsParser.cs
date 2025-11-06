namespace Rca.Tools.MetricsReporter.Configuration;

using System;
using System.Collections.Generic;
using System.Text.Json;
using Rca.Tools.MetricsReporter.Model;
using Rca.Tools.MetricsReporter.Serialization;

/// <summary>
/// Converts a thresholds JSON payload into a <see cref="MetricThreshold"/> dictionary.
/// </summary>
public sealed class ThresholdsParser
{
    /// <summary>
    /// Parses the JSON payload and returns a metric threshold dictionary.
    /// </summary>
    /// <param name="input">JSON payload with thresholds. May be <see langword="null"/>.</param>
    /// <returns>Dictionary with threshold definitions.</returns>
    public IDictionary<MetricIdentifier, MetricThreshold> Parse(string? input)
    {
        var thresholds = CreateDefaults();

        if (string.IsNullOrWhiteSpace(input))
        {
            return thresholds;
        }

        try
        {
            var sanitizedInput = input.Replace('\'', '"');
            var dto = JsonSerializer.Deserialize<Dictionary<string, ThresholdDto>>(sanitizedInput, JsonSerializerOptionsFactory.Create());
            if (dto is null)
            {
                return thresholds;
            }

            foreach (var (key, value) in dto)
            {
                if (!Enum.TryParse<MetricIdentifier>(key, ignoreCase: true, out var identifier))
                {
                    continue;
                }

                thresholds[identifier] = new MetricThreshold
                {
                    Warning = value.Warning,
                    Error = value.Error,
                    HigherIsBetter = value.HigherIsBetter ?? thresholds[identifier].HigherIsBetter
                };
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to parse metrics thresholds JSON.", ex);
        }

        return thresholds;
    }

    private static Dictionary<MetricIdentifier, MetricThreshold> CreateDefaults()
        => new()
        {
            [MetricIdentifier.AltCoverSequenceCoverage] = new MetricThreshold { Warning = 75, Error = 60, HigherIsBetter = true },
            [MetricIdentifier.AltCoverBranchCoverage] = new MetricThreshold { Warning = 70, Error = 55, HigherIsBetter = true },
            [MetricIdentifier.AltCoverCyclomaticComplexity] = new MetricThreshold { Warning = 15, Error = 30, HigherIsBetter = false },
            [MetricIdentifier.AltCoverNPathComplexity] = new MetricThreshold { Warning = 200, Error = 400, HigherIsBetter = false },
            [MetricIdentifier.RoslynMaintainabilityIndex] = new MetricThreshold { Warning = 65, Error = 40, HigherIsBetter = true },
            [MetricIdentifier.RoslynCyclomaticComplexity] = new MetricThreshold { Warning = 12, Error = 25, HigherIsBetter = false },
            [MetricIdentifier.RoslynClassCoupling] = new MetricThreshold { Warning = 50, Error = 80, HigherIsBetter = false },
            [MetricIdentifier.RoslynDepthOfInheritance] = new MetricThreshold { Warning = 5, Error = 8, HigherIsBetter = false },
            [MetricIdentifier.RoslynSourceLines] = new MetricThreshold { HigherIsBetter = false },
            [MetricIdentifier.RoslynExecutableLines] = new MetricThreshold { HigherIsBetter = false },
            [MetricIdentifier.SarifCaRuleViolations] = new MetricThreshold { Warning = 5, Error = 10, HigherIsBetter = false },
            [MetricIdentifier.SarifIdeRuleViolations] = new MetricThreshold { Warning = 10, Error = 20, HigherIsBetter = false }
        };

    private sealed class ThresholdDto
    {
        public decimal? Warning { get; set; }

        public decimal? Error { get; set; }

        public bool? HigherIsBetter { get; set; }
    }
}

