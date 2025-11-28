namespace Rca.Tools.MetricsReporter.Serialization;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Custom converter that keeps backward compatibility for SARIF breakdown dictionaries.
/// </summary>
/// <remarks>
/// Historic reports stored breakdown values as simple integers. Newer reports store
/// <see cref="SarifRuleBreakdownEntry"/> objects with metadata. This converter reads both.
/// </remarks>
internal sealed class SarifBreakdownDictionaryConverter : JsonConverter<Dictionary<string, SarifRuleBreakdownEntry>?>
{
  public override Dictionary<string, SarifRuleBreakdownEntry>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    if (reader.TokenType == JsonTokenType.Null)
    {
      return null;
    }

    if (reader.TokenType != JsonTokenType.StartObject)
    {
      throw new JsonException("Expected breakdown object.");
    }

    var dictionary = new Dictionary<string, SarifRuleBreakdownEntry>(StringComparer.Ordinal);

    while (reader.Read())
    {
      if (reader.TokenType == JsonTokenType.EndObject)
      {
        break;
      }

      if (reader.TokenType != JsonTokenType.PropertyName)
      {
        throw new JsonException("Expected rule identifier property.");
      }

      var ruleId = reader.GetString();
      if (string.IsNullOrWhiteSpace(ruleId))
      {
        reader.Skip();
        continue;
      }

      if (!reader.Read())
      {
        throw new JsonException("Unexpected end of JSON while reading breakdown value.");
      }

      SarifRuleBreakdownEntry entry;
      if (reader.TokenType == JsonTokenType.Number)
      {
        if (!reader.TryGetInt32(out var count))
        {
          throw new JsonException("Legacy breakdown entry must be an integer.");
        }

        entry = new SarifRuleBreakdownEntry
        {
          Count = count,
          Violations = new List<SarifRuleViolationDetail>()
        };
      }
      else if (reader.TokenType == JsonTokenType.StartObject)
      {
        entry = JsonSerializer.Deserialize<SarifRuleBreakdownEntry>(ref reader, options)
            ?? new SarifRuleBreakdownEntry();
      }
      else if (reader.TokenType == JsonTokenType.Null)
      {
        entry = new SarifRuleBreakdownEntry();
      }
      else
      {
        throw new JsonException("Unsupported breakdown entry value. Expected integer or object.");
      }

      dictionary[ruleId] = entry;
    }

    return dictionary;
  }

  public override void Write(Utf8JsonWriter writer, Dictionary<string, SarifRuleBreakdownEntry>? value, JsonSerializerOptions options)
  {
    if (value is null)
    {
      writer.WriteNullValue();
      return;
    }

    writer.WriteStartObject();
    foreach (var pair in value)
    {
      writer.WritePropertyName(pair.Key);
      JsonSerializer.Serialize(writer, pair.Value, options);
    }

    writer.WriteEndObject();
  }
}


