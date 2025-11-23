using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Rca.BuildMetadata.Generator;

/// <summary>
/// Responsible for reading hash values from marker files provided via AdditionalFiles.
/// Follows SRP by isolating hash reading logic from property reading and code generation.
/// </summary>
internal static class HashMarkerReader
{
  /// <summary>
  /// Reads Loader and Runtime hash values from marker files in AdditionalFiles.
  /// Marker files are added by MSBuild target <c>AddHashMarkersToAdditionalFiles</c>
  /// and have names like <c>SourceHash-Loader-&lt;hash&gt;.txt</c> and <c>SourceHash-Runtime-&lt;hash&gt;.txt</c>.
  /// </summary>
  /// <param name="context">The generator execution context providing access to AdditionalFiles.</param>
  /// <param name="isLoaderProject">Whether the current project belongs to the Loader group (for diagnostic reporting).</param>
  /// <param name="isRuntimeProject">Whether the current project belongs to the Runtime group (for diagnostic reporting).</param>
  /// <param name="projectName">The MSBuild project name (for diagnostic reporting).</param>
  /// <returns>
  /// A tuple containing (LoaderHash, RuntimeHash). Either or both may be <see langword="null"/> if marker files are missing.
  /// </returns>
  /// <remarks>
  /// This method searches AdditionalFiles for marker files matching the expected naming pattern.
  /// If marker files are found, their contents (trimmed) are returned as hash values.
  /// Diagnostics are reported for missing or unreadable marker files when the project belongs to a group.
  /// </remarks>
  public static (string? LoaderHash, string? RuntimeHash) ReadHashes(
      GeneratorExecutionContext context,
      bool isLoaderProject,
      bool isRuntimeProject,
      string? projectName)
  {
    var additionalFilesCount = context.AdditionalFiles.Length;

    var loaderHash = ReadHashFromMarker(
        context,
        "Loader",
        "RCA020",
        "Failed to read Loader hash marker file",
        "Failed to read SourceHash-Loader marker file: {0}");

    var runtimeHash = ReadHashFromMarker(
        context,
        "Runtime",
        "RCA021",
        "Failed to read Runtime hash marker file",
        "Failed to read SourceHash-Runtime marker file: {0}");

    ReportMissingGroupHash(
        context,
        isLoaderProject,
        loaderHash,
        "Loader",
        "RCA022",
        projectName,
        additionalFilesCount);

    ReportMissingGroupHash(
        context,
        isRuntimeProject,
        runtimeHash,
        "Runtime",
        "RCA023",
        projectName,
        additionalFilesCount);

    return (loaderHash, runtimeHash);
  }

  private static string? ReadHashFromMarker(
      GeneratorExecutionContext context,
      string groupName,
      string readErrorId,
      string readErrorTitle,
      string readErrorMessageFormat)
  {
    var markerFile = FindMarkerFile(context, groupName);
    if (markerFile == null)
      return null;

    try
    {
      var hashText = markerFile.GetText(context.CancellationToken)?.ToString();
      if (!string.IsNullOrWhiteSpace(hashText))
      {
        return hashText!.Trim();
      }
    }
    catch (Exception ex)
    {
      ReportHashReadFailure(context, readErrorId, readErrorTitle, readErrorMessageFormat, ex.Message);
    }

    return null;
  }

  private static AdditionalText? FindMarkerFile(GeneratorExecutionContext context, string groupName)
  {
    return context.AdditionalFiles.FirstOrDefault(file =>
    {
      var path = file.Path.ToString();
      return path.IndexOf($"SourceHash-{groupName}-", StringComparison.OrdinalIgnoreCase) >= 0 &&
             path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);
    });
  }

  private static void ReportMissingGroupHash(
      GeneratorExecutionContext context,
      bool isGroupProject,
      string? hashValue,
      string groupName,
      string diagnosticId,
      string? projectName,
      int additionalFilesCount)
  {
    if (!isGroupProject || !string.IsNullOrWhiteSpace(hashValue))
      return;

    var descriptor = new DiagnosticDescriptor(
        id: diagnosticId,
        title: $"Missing {groupName} hash marker file",
        messageFormat: $"SourceHash-{groupName} marker file not found in AdditionalFiles for {{0}} project. Found {{1}} additional files.",
        category: "Rca.BuildMetadata.Generator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    context.ReportDiagnostic(
        Diagnostic.Create(
            descriptor,
            Location.None,
            projectName ?? "(unknown)",
            additionalFilesCount));
  }

  private static void ReportHashReadFailure(
      GeneratorExecutionContext context,
      string diagnosticId,
      string title,
      string messageFormat,
      string exceptionMessage)
  {
    var descriptor = new DiagnosticDescriptor(
        id: diagnosticId,
        title: title,
        messageFormat: messageFormat,
        category: "Rca.BuildMetadata.Generator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    context.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None, exceptionMessage));
  }

}

