namespace Rca.Tools.MetricsReporter.Model;

using System.Text.Json.Serialization;

/// <summary>
/// Enumerates all метрики, поддерживаемые единым отчётом.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MetricIdentifier
{
    /// <summary>
    /// Процент покрытых последовательностей AltCover (Sequence Coverage, %).
    /// </summary>
    AltCoverSequenceCoverage,

    /// <summary>
    /// Процент покрытых веток AltCover (Branch Coverage, %).
    /// </summary>
    AltCoverBranchCoverage,

    /// <summary>
    /// Цикломатическая сложность из AltCover/OpenCover (Cyclomatic Complexity AltCover).
    /// </summary>
    AltCoverCyclomaticComplexity,

    /// <summary>
    /// NPath-сложность из AltCover/OpenCover.
    /// </summary>
    AltCoverNPathComplexity,

    /// <summary>
    /// Индекс сопровождаемости Microsoft.CodeAnalysis.Metrics.
    /// </summary>
    RoslynMaintainabilityIndex,

    /// <summary>
    /// Цикломатическая сложность Microsoft.CodeAnalysis.Metrics.
    /// </summary>
    RoslynCyclomaticComplexity,

    /// <summary>
    /// Coupling между классами Microsoft.CodeAnalysis.Metrics.
    /// </summary>
    RoslynClassCoupling,

    /// <summary>
    /// Глубина наследования Microsoft.CodeAnalysis.Metrics.
    /// </summary>
    RoslynDepthOfInheritance,

    /// <summary>
    /// Количество исходных строк кода (Source Lines).
    /// </summary>
    RoslynSourceLines,

    /// <summary>
    /// Количество исполняемых строк кода (Executable Lines).
    /// </summary>
    RoslynExecutableLines,

    /// <summary>
    /// Количество нарушений правил вида CAxxxx, полученных из SARIF.
    /// </summary>
    SarifCaRuleViolations,

    /// <summary>
    /// Количество нарушений правил вида IDExxxx, полученных из SARIF.
    /// </summary>
    SarifIdeRuleViolations,
}

