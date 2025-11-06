namespace Rca.Tools.MetricsReporter.Model;

using System.Text.Json.Serialization;

/// <summary>
/// Тип узла в иерархии отчёта.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CodeElementKind
{
    /// <summary>
    /// Корневой solution.
    /// </summary>
    Solution,

    /// <summary>
    /// Assembly (проект).
    /// </summary>
    Assembly,

    /// <summary>
    /// Пространство имён.
    /// </summary>
    Namespace,

    /// <summary>
    /// Тип (class, struct, record и т.п.).
    /// </summary>
    Type,

    /// <summary>
    /// Член типа (метод, свойство, поле и т.п.).
    /// </summary>
    Member,
}

