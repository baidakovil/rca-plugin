using Microsoft.CodeAnalysis;

namespace Rca.BuildMetadata.Generator;

/// <summary>
/// Main Source Generator that orchestrates reading MSBuild properties, extracting hash values,
/// and generating assembly metadata and build-time constants.
/// </summary>
/// <remarks>
/// This generator follows the SRP by delegating specific responsibilities to focused helper classes:
/// - <see cref="BuildPropertyReader"/> reads and validates MSBuild properties
/// - <see cref="ProjectGroupDetector"/> determines project group membership
/// - <see cref="HashMarkerReader"/> reads hash values from marker files
/// - <see cref="AssemblyMetadataEmitter"/> generates assembly metadata attributes
/// - <see cref="BuildMetadataClassEmitter"/> generates the RcaBuildMetadata class
/// </remarks>
[Generator]
public sealed class BuildMetadataGenerator : ISourceGenerator
{
    /// <summary>
    /// Initializes the Source Generator. No initialization is required at this time.
    /// </summary>
    /// <param name="context">The generator initialization context.</param>
    public void Initialize(GeneratorInitializationContext context)
    {
        // No initialization required
    }

    /// <summary>
    /// Executes the Source Generator to read build properties, extract hashes, and generate code.
    /// </summary>
    /// <param name="context">The generator execution context providing access to MSBuild properties and AdditionalFiles.</param>
    /// <remarks>
    /// Execution flow:
    /// 1. Read and validate all required MSBuild properties (returns early on validation failure)
    /// 2. Detect project group membership (Loader/Runtime)
    /// 3. Read hash values from marker files in AdditionalFiles
    /// 4. Determine effective hash for this project based on group membership
    /// 5. Generate assembly metadata if this is a group project with valid hash and timestamp
    /// 6. Generate RcaBuildMetadata class if this is Rca.Contracts project
    /// </remarks>
    public void Execute(GeneratorExecutionContext context)
    {
        // Step 1: Read and validate all MSBuild properties
        var properties = BuildPropertyReader.ReadProperties(context);
        if (properties == null)
            return; // Validation failed, diagnostics already reported

        // Step 2: Detect project group membership
        var (isLoaderProject, isRuntimeProject) = ProjectGroupDetector.DetectProjectGroup(context);

        // Step 3: Read hash values from marker files
        var (loaderHash, runtimeHash) = HashMarkerReader.ReadHashes(
            context,
            isLoaderProject,
            isRuntimeProject,
            properties.ProjectName);

        // Step 4: Determine effective hash for this project based on group membership
        string? effectiveHash = null;
        if (isLoaderProject)
        {
            effectiveHash = loaderHash;
        }
        else if (isRuntimeProject)
        {
            effectiveHash = runtimeHash;
        }

        // Step 5: Report diagnostics for missing metadata (if this is a group project)
        // Read raw group flags for diagnostic context
        context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.IsLoaderGroupProject", out var isLoaderStr);
        context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.IsRuntimeGroupProject", out var isRuntimeStr);

        AssemblyMetadataEmitter.ReportMissingMetadataDiagnostics(
            context,
            effectiveHash,
            properties.HotReloadTimestamp,
            isLoaderProject,
            isRuntimeProject,
            properties.ProjectName,
            loaderHash,
            runtimeHash,
            isLoaderStr,
            isRuntimeStr);

        // Step 6: Generate assembly metadata if this is a group project with valid data
        AssemblyMetadataEmitter.EmitIfNeeded(
            context,
            effectiveHash,
            properties.HotReloadTimestamp,
            isLoaderProject,
            isRuntimeProject);

        // Step 7: Generate RcaBuildMetadata class if this is Rca.Contracts
        BuildMetadataClassEmitter.EmitIfNeeded(context, properties);
    }
}


