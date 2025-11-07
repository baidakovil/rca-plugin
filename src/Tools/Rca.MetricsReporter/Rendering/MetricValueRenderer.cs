namespace Rca.Tools.MetricsReporter.Rendering;

using System.Net;
using System.Text;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Renders metric values as HTML markup.
/// </summary>
internal static class MetricValueRenderer
{
    /// <summary>
    /// Renders a metric value as HTML, including the value, delta (if any), and appropriate styling.
    /// </summary>
    /// <param name="value">The metric value to render. Can be <see langword="null"/>.</param>
    /// <returns>HTML markup for the metric value.</returns>
    public static string Render(MetricValue? value)
    {
        if (value is null)
        {
            return "<span class=\"metric-value\">-</span>";
        }

        var displayValue = value.Value.HasValue
            ? FormatValue(value.Value.Value, value.Unit)
            : "-";

        var builder = new StringBuilder();
        builder.Append($"<span class=\"metric-value\">{WebUtility.HtmlEncode(displayValue)}</span>");

        if (value.Delta.HasValue && value.Delta.Value != 0)
        {
            var deltaText = value.Delta.Value > 0 ? $"+{value.Delta.Value:0.##}" : $"{value.Delta.Value:0.##}";
            var deltaClass = value.Delta.Value >= 0 ? "delta-positive" : "delta-negative";
            builder.Append($"<sup class=\"{deltaClass}\">{WebUtility.HtmlEncode(deltaText)}</sup>");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Formats a numeric value with the appropriate unit.
    /// </summary>
    /// <param name="value">The numeric value.</param>
    /// <param name="unit">The unit (e.g., "percent").</param>
    /// <returns>Formatted string representation of the value.</returns>
    private static string FormatValue(decimal value, string? unit)
        => unit switch
        {
            "percent" => $"{value:0.##}%",
            _ => $"{value:0.##}"
        };
}

