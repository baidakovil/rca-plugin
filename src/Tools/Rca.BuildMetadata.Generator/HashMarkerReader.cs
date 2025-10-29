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
        string? loaderHash = null;
        string? runtimeHash = null;

        // Find and read Loader hash marker file
        var loaderMarkerFile = context.AdditionalFiles.FirstOrDefault(af =>
        {
            var path = af.Path.ToString();
            return path.IndexOf("SourceHash-Loader-", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);
        });

        if (loaderMarkerFile != null)
        {
            try
            {
                var hashText = loaderMarkerFile.GetText(context.CancellationToken)?.ToString();
                if (!string.IsNullOrWhiteSpace(hashText))
                {
                    loaderHash = hashText!.Trim();
                }
            }
            catch (Exception ex)
            {
                var diag = Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: "RCA020",
                        title: "Failed to read Loader hash marker file",
                        messageFormat: "Failed to read SourceHash-Loader marker file: {0}",
                        category: "Rca.BuildMetadata.Generator",
                        defaultSeverity: DiagnosticSeverity.Warning,
                        isEnabledByDefault: true),
                    Location.None,
                    ex.Message);
                context.ReportDiagnostic(diag);
            }
        }

        // Find and read Runtime hash marker file
        var runtimeMarkerFile = context.AdditionalFiles.FirstOrDefault(af =>
        {
            var path = af.Path.ToString();
            return path.IndexOf("SourceHash-Runtime-", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);
        });

        if (runtimeMarkerFile != null)
        {
            try
            {
                var hashText = runtimeMarkerFile.GetText(context.CancellationToken)?.ToString();
                if (!string.IsNullOrWhiteSpace(hashText))
                {
                    runtimeHash = hashText!.Trim();
                }
            }
            catch (Exception ex)
            {
                var diag = Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: "RCA021",
                        title: "Failed to read Runtime hash marker file",
                        messageFormat: "Failed to read SourceHash-Runtime marker file: {0}",
                        category: "Rca.BuildMetadata.Generator",
                        defaultSeverity: DiagnosticSeverity.Warning,
                        isEnabledByDefault: true),
                    Location.None,
                    ex.Message);
                context.ReportDiagnostic(diag);
            }
        }

        // Report diagnostics if marker files are missing for group projects
        if (isLoaderProject || isRuntimeProject)
        {
            if (isLoaderProject && string.IsNullOrWhiteSpace(loaderHash))
            {
                var diag = Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: "RCA022",
                        title: "Missing Loader hash marker file",
                        messageFormat: "SourceHash-Loader marker file not found in AdditionalFiles for {0} project. Found {1} additional files.",
                        category: "Rca.BuildMetadata.Generator",
                        defaultSeverity: DiagnosticSeverity.Warning,
                        isEnabledByDefault: true),
                    Location.None,
                    projectName ?? "(unknown)",
                    context.AdditionalFiles.Length);
                context.ReportDiagnostic(diag);
            }

            if (isRuntimeProject && string.IsNullOrWhiteSpace(runtimeHash))
            {
                var diag = Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: "RCA023",
                        title: "Missing Runtime hash marker file",
                        messageFormat: "SourceHash-Runtime marker file not found in AdditionalFiles for {0} project. Found {1} additional files.",
                        category: "Rca.BuildMetadata.Generator",
                        defaultSeverity: DiagnosticSeverity.Warning,
                        isEnabledByDefault: true),
                    Location.None,
                    projectName ?? "(unknown)",
                    context.AdditionalFiles.Length);
                context.ReportDiagnostic(diag);
            }
        }

        return (loaderHash, runtimeHash);
    }
}

