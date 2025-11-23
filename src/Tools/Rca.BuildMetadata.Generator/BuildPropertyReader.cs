using System;
using Microsoft.CodeAnalysis;

namespace Rca.BuildMetadata.Generator;

/// <summary>
/// Responsible for reading and validating MSBuild properties from the analyzer config.
/// Follows SRP by isolating property reading logic from code generation.
/// </summary>
internal static class BuildPropertyReader
{
  /// <summary>
  /// Reads all MSBuild properties from the analyzer context and validates them.
  /// Reports diagnostic errors for missing or invalid properties and returns <see langword="null"/> if validation fails.
  /// </summary>
  /// <param name="context">The generator execution context providing access to MSBuild properties.</param>
  /// <returns>
  /// A <see cref="BuildProperties"/> instance with all validated properties, or <see langword="null"/> if validation failed.
  /// When <see langword="null"/> is returned, appropriate diagnostics have been reported via the context.
  /// </returns>
  /// <remarks>
  /// This method reads properties from <c>build_property.*</c> keys in <see cref="AnalyzerConfigOptions.GlobalOptions"/>.
  /// Properties must be made visible to Source Generators via <c>CompilerVisibleProperty</c> items in MSBuild.
  /// Validation ensures that all required properties are present and have valid values before code generation proceeds.
  /// </remarks>
  public static BuildProperties? ReadProperties(GeneratorExecutionContext context)
  {
    if (!TryReadPositiveIntProperty(context, "RcaSourceHashLength", "RCA002", out var length))
      return null;

    if (!TryReadNonEmptyStringProperty(context, "RcaTimestampPattern", "RCA004", out var timestampPattern))
      return null;

    if (!TryReadNonEmptyStringProperty(context, "RcaRevitAddinsDir", "RCA006", out var addinsDir))
      return null;

    if (!TryReadNonEmptyStringProperty(context, "RcaTestDeployRoot", "RCA008", out var testDeployRoot))
      return null;

    if (!TryReadNonEmptyStringProperty(context, "RcaLogRoot", "RCA010", out var logRoot))
      return null;

    if (!TryReadNonEmptyStringProperty(context, "RcaRevitVersion", "RCA012", out var revitVersion))
      return null;

    if (!TryReadNonEmptyStringProperty(context, "RcaRevitLibsPath", "RCA014", out var libsPath))
      return null;

    var pipeName = GetOptionalProperty(context, "RcaCommandPipeName");
    var logPipeName = GetOptionalProperty(context, "RcaLogPipeName");

    if (!TryGetRequiredProperty(context, "RcaTimestampFile", out var timestampFile))
      return null;

    if (!TryReadNonNegativeIntProperty(context, "RcaStickyStampSeconds", "RCA017", out var stickySeconds))
      return null;

    var forceNewStamp = ReadBooleanFlag(GetOptionalProperty(context, "RcaForceNewStamp"));
    var loaderProjects = GetOptionalProperty(context, "RcaLoaderProjectsList");
    var runtimeProjects = GetOptionalProperty(context, "RcaRuntimeProjectsList");
    var projectName = GetOptionalProperty(context, "MSBuildProjectName");
    var hotReloadTimestamp = GetOptionalProperty(context, "RcaHotReloadTimestamp");

    var properties = new BuildProperties
    {
      SourceHashLength = length,
      TimestampPattern = timestampPattern,
      RevitAddinsDir = addinsDir,
      TestDeployRoot = testDeployRoot,
      LogRoot = logRoot,
      RevitVersion = revitVersion,
      RevitLibsPath = libsPath,
      CommandPipeName = pipeName,
      LogPipeName = logPipeName,
      TimestampFile = timestampFile!,
      StickyStampSeconds = stickySeconds,
      ForceNewStamp = forceNewStamp,
      LoaderProjectsList = loaderProjects,
      RuntimeProjectsList = runtimeProjects,
      ProjectName = projectName,
      HotReloadTimestamp = hotReloadTimestamp
    };
    return properties;
  }

  /// <summary>
  /// Attempts to read a required MSBuild property from the analyzer context.
  /// Reports a diagnostic error if the property is missing.
  /// </summary>
  /// <param name="context">The generator execution context.</param>
  /// <param name="propertyName">The MSBuild property name (without "build_property." prefix).</param>
  /// <param name="value">When this method returns, contains the property value if found; otherwise <see langword="null"/>.</param>
  /// <returns><see langword="true"/> if the property was found; otherwise <see langword="false"/>.</returns>
  private static bool TryGetRequiredProperty(GeneratorExecutionContext context, string propertyName, out string? value)
  {
    var fullKey = $"build_property.{propertyName}";
    if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue(fullKey, out value))
      return true;

    ReportError(context, GetErrorCodeForProperty(propertyName), $"Missing build property: {propertyName}",
        $"MSBuild property '{propertyName}' must be defined (e.g. in Directory.Build.props).");
    return false;
  }

  private static bool TryReadPositiveIntProperty(GeneratorExecutionContext context, string propertyName, string errorCode, out int value)
  {
    return TryReadIntProperty(context, propertyName, v => v > 0, "a positive integer", errorCode, out value);
  }

  private static bool TryReadNonNegativeIntProperty(GeneratorExecutionContext context, string propertyName, string errorCode, out int value)
  {
    return TryReadIntProperty(context, propertyName, v => v >= 0, "a non-negative integer", errorCode, out value);
  }

  private static bool TryReadIntProperty(
      GeneratorExecutionContext context,
      string propertyName,
      Func<int, bool> validator,
      string requirementDescription,
      string errorCode,
      out int value)
  {
    if (!TryGetRequiredProperty(context, propertyName, out var rawValue))
    {
      value = 0;
      return false;
    }

    if (!int.TryParse(rawValue, out value) || !validator(value))
    {
      ReportError(
          context,
          errorCode,
          $"Invalid build property: {propertyName}",
          $"MSBuild property '{propertyName}' must be {requirementDescription}. Current value: '{FormatValue(rawValue)}'");
      value = 0;
      return false;
    }

    return true;
  }

  private static bool TryReadNonEmptyStringProperty(GeneratorExecutionContext context, string propertyName, string invalidErrorCode, out string value)
  {
    if (!TryGetRequiredProperty(context, propertyName, out var rawValue))
    {
      value = string.Empty;
      return false;
    }

    if (string.IsNullOrWhiteSpace(rawValue))
    {
      ReportError(
          context,
          invalidErrorCode,
          $"Invalid build property: {propertyName}",
          $"MSBuild property '{propertyName}' must be a non-empty string. Current value: '{FormatValue(rawValue)}'");
      value = string.Empty;
      return false;
    }

    value = rawValue!;
    return true;
  }

  private static string? GetOptionalProperty(GeneratorExecutionContext context, string propertyName)
  {
    context.AnalyzerConfigOptions.GlobalOptions.TryGetValue($"build_property.{propertyName}", out var value);
    return value;
  }

  private static bool ReadBooleanFlag(string? rawValue)
  {
    if (string.IsNullOrWhiteSpace(rawValue))
      return false;

    var normalized = rawValue!.Trim();
    return normalized.Equals("true", StringComparison.OrdinalIgnoreCase) ||
           normalized.Equals("1", StringComparison.OrdinalIgnoreCase);
  }

  private static string FormatValue(string? value) => value ?? "(null)";

  /// <summary>
  /// Gets the diagnostic error code for a missing property based on property name conventions.
  /// </summary>
  private static string GetErrorCodeForProperty(string propertyName)
  {
    // Map property names to error codes (RCA001-RCA017 range)
    return propertyName switch
    {
      "RcaSourceHashLength" => "RCA001",
      "RcaTimestampPattern" => "RCA003",
      "RcaRevitAddinsDir" => "RCA005",
      "RcaTestDeployRoot" => "RCA007",
      "RcaLogRoot" => "RCA009",
      "RcaRevitVersion" => "RCA011",
      "RcaRevitLibsPath" => "RCA013",
      "RcaTimestampFile" => "RCA015",
      "RcaStickyStampSeconds" => "RCA016",
      _ => "RCA001" // Fallback
    };
  }

  /// <summary>
  /// Reports a diagnostic error with the specified code, title, and message.
  /// </summary>
  private static void ReportError(GeneratorExecutionContext context, string code, string title, string message)
  {
    var diag = Diagnostic.Create(
        new DiagnosticDescriptor(
            id: code,
            title: title,
            messageFormat: message,
            category: "Rca.BuildMetadata.Generator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true),
        Location.None);
    context.ReportDiagnostic(diag);
  }
}

