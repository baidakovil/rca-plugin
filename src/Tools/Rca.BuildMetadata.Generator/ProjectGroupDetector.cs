using Microsoft.CodeAnalysis;

namespace Rca.BuildMetadata.Generator;

/// <summary>
/// Responsible for detecting which project group (Loader or Runtime) the current project belongs to.
/// Follows SRP by isolating group detection logic from other concerns.
/// </summary>
internal static class ProjectGroupDetector
{
    /// <summary>
    /// Determines whether the current project belongs to the Loader or Runtime group based on MSBuild properties.
    /// </summary>
    /// <param name="context">The generator execution context providing access to MSBuild properties.</param>
    /// <returns>
    /// A tuple indicating group membership: (IsLoaderProject, IsRuntimeProject).
    /// Both flags can be false if the project does not belong to either group.
    /// </returns>
    /// <remarks>
    /// Group membership is determined by reading <c>IsLoaderGroupProject</c> and <c>IsRuntimeGroupProject</c>
    /// MSBuild properties that are set by <c>Directory.Build.targets</c> based on project name matching.
    /// </remarks>
    public static (bool IsLoaderProject, bool IsRuntimeProject) DetectProjectGroup(GeneratorExecutionContext context)
    {
        context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.IsLoaderGroupProject", out var isLoaderStr);
        context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.IsRuntimeGroupProject", out var isRuntimeStr);

        var isLoaderProject = string.Equals(isLoaderStr, "true", StringComparison.OrdinalIgnoreCase);
        var isRuntimeProject = string.Equals(isRuntimeStr, "true", StringComparison.OrdinalIgnoreCase);

        return (isLoaderProject, isRuntimeProject);
    }
}

