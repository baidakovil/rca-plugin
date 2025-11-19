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
    // Read and validate SourceHashLength (required, positive integer)
    if (!TryGetRequiredProperty(context, "RcaSourceHashLength", out var lengthStr))
      return null;

    if (!int.TryParse(lengthStr, out var length) || length <= 0)
    {
      ReportError(context, "RCA002", "Invalid build property: RcaSourceHashLength",
          $"MSBuild property 'RcaSourceHashLength' must be a positive integer. Current value: '{lengthStr ?? "(null)"}'");
      return null;
    }

    // Read and validate TimestampPattern (required, non-empty)
    if (!TryGetRequiredProperty(context, "RcaTimestampPattern", out var patternStr))
      return null;

    if (string.IsNullOrWhiteSpace(patternStr))
    {
      ReportError(context, "RCA004", "Invalid build property: RcaTimestampPattern",
          $"MSBuild property 'RcaTimestampPattern' must be a non-empty string. Current value: '{patternStr ?? "(null)"}'");
      return null;
    }

    // Read and validate string properties (required, non-empty)
    if (!TryGetRequiredProperty(context, "RcaRevitAddinsDir", out var addinsDir))
      return null;

    if (string.IsNullOrWhiteSpace(addinsDir))
    {
      ReportError(context, "RCA006", "Invalid build property: RcaRevitAddinsDir",
          $"MSBuild property 'RcaRevitAddinsDir' must be a non-empty string. Current value: '{addinsDir ?? "(null)"}'");
      return null;
    }

    if (!TryGetRequiredProperty(context, "RcaTestDeployRoot", out var testDeployRoot))
      return null;

    if (string.IsNullOrWhiteSpace(testDeployRoot))
    {
      ReportError(context, "RCA008", "Invalid build property: RcaTestDeployRoot",
          $"MSBuild property 'RcaTestDeployRoot' must be a non-empty string. Current value: '{testDeployRoot ?? "(null)"}'");
      return null;
    }

    if (!TryGetRequiredProperty(context, "RcaLogRoot", out var logRoot))
      return null;

    if (string.IsNullOrWhiteSpace(logRoot))
    {
      ReportError(context, "RCA010", "Invalid build property: RcaLogRoot",
          $"MSBuild property 'RcaLogRoot' must be a non-empty string. Current value: '{logRoot ?? "(null)"}'");
      return null;
    }

    if (!TryGetRequiredProperty(context, "RcaRevitVersion", out var revitVersion))
      return null;

    if (string.IsNullOrWhiteSpace(revitVersion))
    {
      ReportError(context, "RCA012", "Invalid build property: RcaRevitVersion",
          $"MSBuild property 'RcaRevitVersion' must be a non-empty string. Current value: '{revitVersion ?? "(null)"}'");
      return null;
    }

    if (!TryGetRequiredProperty(context, "RcaRevitLibsPath", out var libsPath))
      return null;

    if (string.IsNullOrWhiteSpace(libsPath))
    {
      ReportError(context, "RCA014", "Invalid build property: RcaRevitLibsPath",
          $"MSBuild property 'RcaRevitLibsPath' must be a non-empty string. Current value: '{libsPath ?? "(null)"}'");
      return null;
    }

    // Read optional pipe names
    context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RcaCommandPipeName", out var pipeName);
    context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RcaLogPipeName", out var logPipeName);

    // Read and validate timestamp file (required, non-empty)
    if (!TryGetRequiredProperty(context, "RcaTimestampFile", out var timestampFile))
      return null;

    // Read and validate StickyStampSeconds (required, non-negative integer)
    if (!TryGetRequiredProperty(context, "RcaStickyStampSeconds", out var stickyStr))
      return null;

    if (!int.TryParse(stickyStr, out var stickySeconds) || stickySeconds < 0)
    {
      ReportError(context, "RCA017", "Invalid build property: RcaStickyStampSeconds",
          $"MSBuild property 'RcaStickyStampSeconds' must be a non-negative integer. Current value: '{stickyStr ?? "(null)"}'");
      return null;
    }

    // Read ForceNewStamp (optional, defaults to false)
    context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RcaForceNewStamp", out var forceStr);
    var forceNewStamp = false;
    if (!string.IsNullOrWhiteSpace(forceStr))
    {
      var tmp = forceStr!;
      var lower = tmp.Trim().ToLowerInvariant();
      forceNewStamp = lower == "true" || lower == "1";
    }

    // Read optional project lists (may be empty for non-group projects)
    context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RcaLoaderProjectsList", out var loaderProjects);
    context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RcaRuntimeProjectsList", out var runtimeProjects);

    // Read project name (optional, used for diagnostics)
    context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.MSBuildProjectName", out var projectName);

    // Read hot-reload timestamp (optional, may be empty during parallel builds)
    context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RcaHotReloadTimestamp", out var hotReloadTimestamp);

    // At this point, all required string properties have been validated to be non-empty
    // Use null-forgiving operator to satisfy nullable analysis after validation
    var properties = new BuildProperties
    {
      SourceHashLength = length,
      TimestampPattern = patternStr!,
      RevitAddinsDir = addinsDir!,
      TestDeployRoot = testDeployRoot!,
      LogRoot = logRoot!,
      RevitVersion = revitVersion!,
      RevitLibsPath = libsPath!,
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

