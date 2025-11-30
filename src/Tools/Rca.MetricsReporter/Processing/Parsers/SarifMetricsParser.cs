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
/// Parses SARIF output produced by Roslyn analyzers.
/// </summary>
public sealed class SarifMetricsParser : IMetricsSourceParser
{
  /// <inheritdoc />
  public async Task<ParsedMetricsDocument> ParseAsync(string path, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(path);

    using var document = await ReadJsonDocumentAsync(path, cancellationToken).ConfigureAwait(false);
    return ProcessDocument(document);
  }

  /// <summary>
  /// Reads and parses a JSON document from a file path.
  /// </summary>
  /// <param name="path">Path to the JSON file.</param>
  /// <param name="cancellationToken">Cancellation token for async operations.</param>
  /// <returns>The parsed JSON document.</returns>
  private static async Task<JsonDocument> ReadJsonDocumentAsync(string path, CancellationToken cancellationToken)
  {
    await using var stream = File.OpenRead(path);
    return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
  }

  /// <summary>
  /// Processes a parsed JSON document and extracts code elements.
  /// </summary>
  /// <param name="document">The parsed JSON document.</param>
  /// <returns>A parsed metrics document containing extracted elements.</returns>
  private static ParsedMetricsDocument ProcessDocument(JsonDocument document)
  {
    if (!document.RootElement.TryGetProperty("runs", out var runs) || runs.ValueKind != JsonValueKind.Array)
    {
      return EmptyDocument();
    }

    var elements = new List<ParsedCodeElement>();
    var ruleDescriptions = new Dictionary<string, RuleDescription>();

    foreach (var run in runs.EnumerateArray())
    {
      elements.AddRange(ParseRun(run));
      ExtractRuleDescriptions(run, ruleDescriptions);
    }

    return new ParsedMetricsDocument
    {
      SolutionName = string.Empty,
      Elements = elements,
      RuleDescriptions = ruleDescriptions
    };
  }

  private static IEnumerable<ParsedCodeElement> ParseRun(JsonElement run)
  {
    if (!run.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
    {
      yield break;
    }

    foreach (var result in results.EnumerateArray())
    {
      foreach (var element in ProcessResult(result))
      {
        yield return element;
      }
    }
  }

  /// <summary>
  /// Processes a single SARIF result and yields code elements for each location.
  /// </summary>
  /// <param name="result">The SARIF result JSON element.</param>
  /// <returns>An enumerable of parsed code elements.</returns>
  private static IEnumerable<ParsedCodeElement> ProcessResult(JsonElement result)
  {
    var ruleId = result.GetPropertyOrDefault("ruleId")?.GetString();
    if (ruleId is null)
    {
      yield break;
    }

    if (!TryResolveMetric(ruleId, out var identifier))
    {
      yield break;
    }

    if (!TryGetPrimaryLocation(result, out var location))
    {
      yield break;
    }

    var messageText = result.GetPropertyOrDefault("message")
        ?.GetPropertyOrDefault("text")
        ?.GetString();

    yield return CreateCodeElement(ruleId, identifier, location, messageText);
  }

  /// <summary>
  /// Creates a parsed code element from rule ID, metric identifier, and source location.
  /// </summary>
  /// <param name="ruleId">The SARIF rule identifier.</param>
  /// <param name="identifier">The resolved metric identifier.</param>
  /// <param name="location">The source location of the violation.</param>
  /// <returns>A parsed code element representing the violation.</returns>
  private static ParsedCodeElement CreateCodeElement(
      string ruleId,
      MetricIdentifier identifier,
      SarifLocation location,
      string? messageText)
  {
    // WHY: We create a breakdown dictionary only for SARIF metrics to track individual rule violations.
    // This allows the report to show which specific rules (CA1502, IDE0051, etc.) are violated
    // and in what quantity, not just the total count. We validate the ruleId to ensure
    // only properly formatted rule IDs are stored, preventing schema violations.
    Dictionary<string, SarifRuleBreakdownEntry>? breakdown = null;
    if (RuleIdValidator.IsValidRuleId(ruleId))
    {
      breakdown = new Dictionary<string, SarifRuleBreakdownEntry>(StringComparer.Ordinal)
      {
        [ruleId] = new SarifRuleBreakdownEntry
        {
          Count = 1,
          Violations = []
        }
      };
    }

    return new ParsedCodeElement(CodeElementKind.Member, ruleId, null)
    {
      Metrics = new Dictionary<MetricIdentifier, MetricValue>
      {
        [identifier] = new MetricValue
        {
          Value = 1,
          Status = ThresholdStatus.NotApplicable,
          Breakdown = breakdown
        }
      },
      Source = location.Source
    };
  }

  /// <summary>
  /// Extracts rule descriptions from a SARIF run element.
  /// </summary>
  /// <param name="run">The SARIF run JSON element.</param>
  /// <param name="ruleDescriptions">Dictionary to populate with extracted rule descriptions.</param>
  private static void ExtractRuleDescriptions(JsonElement run, Dictionary<string, RuleDescription> ruleDescriptions)
  {
    var tool = run.GetPropertyOrDefault("tool");
    if (tool is null)
    {
      return;
    }

    var driver = tool.Value.GetPropertyOrDefault("driver");
    if (driver is null)
    {
      return;
    }

    var rules = driver.Value.GetPropertyOrDefault("rules");
    if (rules is null || rules.Value.ValueKind != JsonValueKind.Array)
    {
      return;
    }

    foreach (var rule in rules.Value.EnumerateArray())
    {
      var ruleId = rule.GetPropertyOrDefault("id")?.GetString();
      if (string.IsNullOrWhiteSpace(ruleId))
      {
        continue;
      }

      // Only extract descriptions for CA and IDE rules
      if (!TryResolveMetric(ruleId, out _))
      {
        continue;
      }

      var shortDescription = rule.GetPropertyOrDefault("shortDescription")?.GetPropertyOrDefault("text")?.GetString() ?? string.Empty;
      var fullDescription = rule.GetPropertyOrDefault("fullDescription")?.GetPropertyOrDefault("text")?.GetString();
      var helpUri = rule.GetPropertyOrDefault("helpUri")?.GetString();
      var category = rule.GetPropertyOrDefault("properties")?.GetPropertyOrDefault("category")?.GetString();

      var description = new RuleDescription
      {
        ShortDescription = shortDescription,
        FullDescription = fullDescription,
        HelpUri = helpUri,
        Category = category
      };

      ruleDescriptions[ruleId] = description;
    }
  }

  private static ParsedMetricsDocument EmptyDocument()
      => new()
      {
        SolutionName = string.Empty,
        Elements = Array.Empty<ParsedCodeElement>(),
        RuleDescriptions = new Dictionary<string, RuleDescription>()
      };

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

  private static bool TryGetPrimaryLocation(JsonElement result, out SarifLocation location)
  {
    location = default!;
    if (!result.TryGetProperty("locations", out var locations) || locations.ValueKind != JsonValueKind.Array)
    {
      return false;
    }

    foreach (var entry in locations.EnumerateArray())
    {
      if (!entry.TryGetProperty("physicalLocation", out var physicalLocation) || physicalLocation.ValueKind != JsonValueKind.Object)
      {
        continue;
      }

      var uriElement = physicalLocation.GetPropertyOrDefault("artifactLocation")?.GetPropertyOrDefault("uri");
      var uri = uriElement?.GetString();
      if (uri is null)
      {
        continue;
      }

      var resolvedPath = NormalizePath(uri);
      var region = physicalLocation.GetPropertyOrDefault("region");

      var startLine = TryGetLine(region, "startLine");
      var endLine = TryGetLine(region, "endLine") ?? startLine;

      var sourceLocation = new SourceLocation
      {
        Path = resolvedPath,
        StartLine = startLine,
        EndLine = endLine
      };

      location = new SarifLocation(sourceLocation, uri);
      return true;
    }

    return false;
  }

  private static int? TryGetLine(JsonElement? region, string propertyName)
      => region is not null && region.Value.TryGetIntProperty(propertyName, out var line)
          ? line
          : null;

  private static string NormalizePath(string path)
  {
    if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsFile)
    {
      return uri.LocalPath;
    }

    return path.Replace('/', Path.DirectorySeparatorChar);
  }

  private sealed record SarifLocation(SourceLocation Source, string? OriginalUri);
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

