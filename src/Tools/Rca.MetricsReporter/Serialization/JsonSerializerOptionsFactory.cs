namespace Rca.Tools.MetricsReporter.Serialization;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Создаёт преднастроенные JSON-опции для (де)сериализации отчётов.
/// </summary>
public static class JsonSerializerOptionsFactory
{
    /// <summary>
    /// Возвращает опции сериализации JSON с camelCase и поддержкой enum-конвертера.
    /// </summary>
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

