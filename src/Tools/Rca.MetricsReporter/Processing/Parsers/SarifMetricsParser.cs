namespace Rca.Tools.MetricsReporter.Processing.Parsers;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Парсер SARIF результатов Roslyn анализаторов.
/// </summary>
public sealed class SarifMetricsParser : IMetricsSourceParser
{
    /// <inheritdoc />
    public async Task<ParsedMetricsDocument> ParseAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);

        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("runs", out var runs) || runs.ValueKind != JsonValueKind.Array)
        {
            return new ParsedMetricsDocument
            {
                SolutionName = string.Empty,
                Elements = Array.Empty<ParsedCodeElement>()
            };
        }

        var elements = new List<ParsedCodeElement>();

        foreach (var run in runs.EnumerateArray())
        {
            if (!run.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var result in results.EnumerateArray())
            {
                var ruleId = result.GetPropertyOrDefault("ruleId")?.GetString();
                if (ruleId is null)
                {
                    continue;
                }

                if (!TryResolveMetric(ruleId, out var identifier))
                {
                    continue;
                }

                foreach (var location in EnumerateLocations(result))
                {
                    var node = new ParsedCodeElement(CodeElementKind.Member, ruleId, null)
                    {
                        Metrics = new Dictionary<MetricIdentifier, MetricValue>
                        {
                            [identifier] = new MetricValue
                            {
                                Value = 1,
                                Unit = "count",
                                Status = ThresholdStatus.NotApplicable
                            }
                        },
                        Source = location,
                        ParentFullyQualifiedName = null
                    };

                    elements.Add(node);
                }
            }
        }

        return new ParsedMetricsDocument
        {
            SolutionName = string.Empty,
            Elements = elements
        };
    }

    private static bool TryResolveMetric(string ruleId, out MetricIdentifier identifier)
    {
        if (ruleId.StartsWith("CA", StringComparison.OrdinalIgnoreCase))
        {
            identifier = MetricIdentifier.SarifCaRuleViolations;
            return true;
        }

        if (ruleId.StartsWith("IDE", StringComparison.OrdinalIgnoreCase))
        {
            identifier = MetricIdentifier.SarifIdeRuleViolations;
            return true;
        }

        identifier = default;
        return false;
    }

    private static IEnumerable<SourceLocation> EnumerateLocations(JsonElement result)
    {
        if (!result.TryGetProperty("locations", out var locations) || locations.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var location in locations.EnumerateArray())
        {
            if (!location.TryGetProperty("physicalLocation", out var physicalLocation) || physicalLocation.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var uriElement = physicalLocation.GetPropertyOrDefault("artifactLocation")?.GetPropertyOrDefault("uri");
            var path = uriElement?.GetString();
            if (path is null)
            {
                continue;
            }

            var resolvedPath = NormalizePath(path);
            var region = physicalLocation.GetPropertyOrDefault("region");

            int? startLine = null;
            int? endLine = null;

            if (region is not null)
            {
                if (region.Value.TryGetIntProperty("startLine", out var sLine))
                {
                    startLine = sLine;
                }

                if (region.Value.TryGetIntProperty("endLine", out var eLine))
                {
                    endLine = eLine;
                }
            }

            yield return new SourceLocation
            {
                Path = resolvedPath,
                StartLine = startLine,
                EndLine = endLine ?? startLine
            };
        }
    }

    private static string NormalizePath(string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            return uri.LocalPath;
        }

        return path.Replace('/', Path.DirectorySeparatorChar);
    }
}

file static class JsonElementExtensions
{
    public static JsonElement? GetPropertyOrDefault(this JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) ? property : null;

    public static bool TryGetIntProperty(this JsonElement element, string propertyName, out int value)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number)
        {
            return property.TryGetInt32(out value);
        }

        value = default;
        return false;
    }
}

