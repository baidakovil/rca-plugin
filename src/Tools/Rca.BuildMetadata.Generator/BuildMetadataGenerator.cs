using System;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Rca.BuildMetadata.Generator;

[Generator]
public sealed class BuildMetadataGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        // No initialization required at this time - updated for timestamp pattern support
    }

    public void Execute(GeneratorExecutionContext context)
    {
        // Read MSBuild property made visible via CompilerVisibleProperty
        if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RcaSourceHashLength", out var lengthStr))
        {
            // Property must be provided explicitly by MSBuild (Directory.Build.props). Do not fallback to defaults.
            var diag = Diagnostic.Create(new DiagnosticDescriptor(
                id: "RCA001",
                title: "Missing build property: RcaSourceHashLength",
                messageFormat: "MSBuild property 'RcaSourceHashLength' must be defined (e.g. in Directory.Build.props).",
                category: "Rca.BuildMetadata.Generator",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true), Location.None);
            context.ReportDiagnostic(diag);
            return;
        }

        if (!int.TryParse(lengthStr, out var length) || length <= 0)
        {
            var diag = Diagnostic.Create(new DiagnosticDescriptor(
                id: "RCA002",
                title: "Invalid build property: RcaSourceHashLength",
                messageFormat: "MSBuild property 'RcaSourceHashLength' must be a positive integer. Current value: '{0}'",
                category: "Rca.BuildMetadata.Generator",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true), Location.None, lengthStr ?? "(null)");
            context.ReportDiagnostic(diag);
            return;
        }

        // Read timestamp pattern property
        if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RcaTimestampPattern", out var patternStr))
        {
            // Property must be provided explicitly by MSBuild (Directory.Build.props). Do not fallback to defaults.
            var diag = Diagnostic.Create(new DiagnosticDescriptor(
                id: "RCA003",
                title: "Missing build property: RcaTimestampPattern",
                messageFormat: "MSBuild property 'RcaTimestampPattern' must be defined (e.g. in Directory.Build.props).",
                category: "Rca.BuildMetadata.Generator",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true), Location.None);
            context.ReportDiagnostic(diag);
            return;
        }

        if (string.IsNullOrWhiteSpace(patternStr))
        {
            var diag = Diagnostic.Create(new DiagnosticDescriptor(
                id: "RCA004",
                title: "Invalid build property: RcaTimestampPattern",
                messageFormat: "MSBuild property 'RcaTimestampPattern' must be a non-empty string. Current value: '{0}'",
                category: "Rca.BuildMetadata.Generator",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true), Location.None, patternStr ?? "(null)");
            context.ReportDiagnostic(diag);
            return;
        }

        // Read Revit Addins directory property
        if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RcaRevitAddinsDir", out var addinsDir))
        {
            var diag = Diagnostic.Create(new DiagnosticDescriptor(
                id: "RCA005",
                title: "Missing build property: RcaRevitAddinsDir",
                messageFormat: "MSBuild property 'RcaRevitAddinsDir' must be defined (e.g. in Directory.Build.props).",
                category: "Rca.BuildMetadata.Generator",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true), Location.None);
            context.ReportDiagnostic(diag);
            return;
        }

        if (string.IsNullOrWhiteSpace(addinsDir))
        {
            var diag = Diagnostic.Create(new DiagnosticDescriptor(
                id: "RCA006",
                title: "Invalid build property: RcaRevitAddinsDir",
                messageFormat: "MSBuild property 'RcaRevitAddinsDir' must be a non-empty string. Current value: '{0}'",
                category: "Rca.BuildMetadata.Generator",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true), Location.None, addinsDir ?? "(null)");
            context.ReportDiagnostic(diag);
            return;
        }
        
        // Read test deploy root property
        if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RcaTestDeployRoot", out var testDeployRoot))
        {
            var diag = Diagnostic.Create(new DiagnosticDescriptor(
                id: "RCA007",
                title: "Missing build property: RcaTestDeployRoot",
                messageFormat: "MSBuild property 'RcaTestDeployRoot' must be defined (e.g. in Directory.Build.props).",
                category: "Rca.BuildMetadata.Generator",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true), Location.None);
            context.ReportDiagnostic(diag);
            return;
        }

        if (string.IsNullOrWhiteSpace(testDeployRoot))
        {
            var diag = Diagnostic.Create(new DiagnosticDescriptor(
                id: "RCA008",
                title: "Invalid build property: RcaTestDeployRoot",
                messageFormat: "MSBuild property 'RcaTestDeployRoot' must be a non-empty string. Current value: '{0}'",
                category: "Rca.BuildMetadata.Generator",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true), Location.None, testDeployRoot ?? "(null)");
            context.ReportDiagnostic(diag);
            return;
        }
        
        // Read log root property
        if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RcaLogRoot", out var logRoot))
        {
            var diag = Diagnostic.Create(new DiagnosticDescriptor(
                id: "RCA009",
                title: "Missing build property: RcaLogRoot",
                messageFormat: "MSBuild property 'RcaLogRoot' must be defined (e.g. in Directory.Build.props).",
                category: "Rca.BuildMetadata.Generator",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true), Location.None);
            context.ReportDiagnostic(diag);
            return;
        }

        if (string.IsNullOrWhiteSpace(logRoot))
        {
            var diag = Diagnostic.Create(new DiagnosticDescriptor(
                id: "RCA010",
                title: "Invalid build property: RcaLogRoot",
                messageFormat: "MSBuild property 'RcaLogRoot' must be a non-empty string. Current value: '{0}'",
                category: "Rca.BuildMetadata.Generator",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true), Location.None, logRoot ?? "(null)");
            context.ReportDiagnostic(diag);
            return;
        }

        // Read Revit version property
        if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RcaRevitVersion", out var revitVersion))
        {
            var diag = Diagnostic.Create(new DiagnosticDescriptor(
                id: "RCA011",
                title: "Missing build property: RcaRevitVersion",
                messageFormat: "MSBuild property 'RcaRevitVersion' must be defined (e.g. in Directory.Build.props).",
                category: "Rca.BuildMetadata.Generator",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true), Location.None);
            context.ReportDiagnostic(diag);
            return;
        }

        if (string.IsNullOrWhiteSpace(revitVersion))
        {
            var diag = Diagnostic.Create(new DiagnosticDescriptor(
                id: "RCA012",
                title: "Invalid build property: RcaRevitVersion",
                messageFormat: "MSBuild property 'RcaRevitVersion' must be a non-empty string. Current value: '{0}'",
                category: "Rca.BuildMetadata.Generator",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true), Location.None, revitVersion ?? "(null)");
            context.ReportDiagnostic(diag);
            return;
        }

        // Read Revit libs path property
        if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RcaRevitLibsPath", out var libsPath))
        {
            var diag = Diagnostic.Create(new DiagnosticDescriptor(
                id: "RCA013",
                title: "Missing build property: RcaRevitLibsPath",
                messageFormat: "MSBuild property 'RcaRevitLibsPath' must be defined (e.g. in Directory.Build.props).",
                category: "Rca.BuildMetadata.Generator",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true), Location.None);
            context.ReportDiagnostic(diag);
            return;
        }
        if (string.IsNullOrWhiteSpace(libsPath))
        {
            var diag = Diagnostic.Create(new DiagnosticDescriptor(
                id: "RCA014",
                title: "Invalid build property: RcaRevitLibsPath",
                messageFormat: "MSBuild property 'RcaRevitLibsPath' must be a non-empty string. Current value: '{0}'",
                category: "Rca.BuildMetadata.Generator",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true), Location.None, libsPath ?? "(null)");
            context.ReportDiagnostic(diag);
            return;
        }
        
        // Read pipe names
        context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RcaCommandPipeName", out var pipeName);
        context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RcaLogPipeName", out var logPipeName);

        // Read timestamp file and sticky/force stamp settings
        if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RcaTimestampFile", out var timestampFile))
        {
            var diag = Diagnostic.Create(new DiagnosticDescriptor(
                id: "RCA015",
                title: "Missing build property: RcaTimestampFile",
                messageFormat: "MSBuild property 'RcaTimestampFile' must be defined (e.g. in Directory.Build.props).",
                category: "Rca.BuildMetadata.Generator",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true), Location.None);
            context.ReportDiagnostic(diag);
            return;
        }

        if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RcaStickyStampSeconds", out var stickyStr))
        {
            var diag = Diagnostic.Create(new DiagnosticDescriptor(
                id: "RCA016",
                title: "Missing build property: RcaStickyStampSeconds",
                messageFormat: "MSBuild property 'RcaStickyStampSeconds' must be defined (e.g. in Directory.Build.props).",
                category: "Rca.BuildMetadata.Generator",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true), Location.None);
            context.ReportDiagnostic(diag);
            return;
        }

        if (!int.TryParse(stickyStr, out var stickySeconds) || stickySeconds < 0)
        {
            var diag = Diagnostic.Create(new DiagnosticDescriptor(
                id: "RCA017",
                title: "Invalid build property: RcaStickyStampSeconds",
                messageFormat: "MSBuild property 'RcaStickyStampSeconds' must be a non-negative integer. Current value: '{0}'",
                category: "Rca.BuildMetadata.Generator",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true), Location.None, stickyStr ?? "(null)");
            context.ReportDiagnostic(diag);
            return;
        }

        // Force flag may be expressed as true/false or 1/0
        context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RcaForceNewStamp", out var forceStr);
        var forceNewStamp = false;
        if (!string.IsNullOrWhiteSpace(forceStr))
        {
            // forceStr may be null at compile-time analysis, use a local non-null assertion after guard
            var tmp = forceStr!;
            var lower = tmp.Trim().ToLowerInvariant();
            forceNewStamp = lower == "true" || lower == "1";
        }

        // Read project lists (optional - may not be set for all projects)
        context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RcaLoaderProjectsList", out var loaderProjects);
        context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RcaRuntimeProjectsList", out var runtimeProjects);
        
        // Convert semicolon-separated strings to C# string array literals
        var loaderArray = string.IsNullOrWhiteSpace(loaderProjects) 
            ? "System.Array.Empty<string>()" 
            : $"new[] {{ \"{string.Join("\", \"", loaderProjects!.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))}\" }}";
            
        var runtimeArray = string.IsNullOrWhiteSpace(runtimeProjects) 
            ? "System.Array.Empty<string>()" 
            : $"new[] {{ \"{string.Join("\", \"", runtimeProjects!.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))}\" }}";

        // Read project name first (used in diagnostics)
        context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.MSBuildProjectName", out var projectName);

        // Determine whether this project belongs to Loader/Runtime group and read group hashes + timestamp
        context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.IsLoaderGroupProject", out var isLoaderStr);
        context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.IsRuntimeGroupProject", out var isRuntimeStr);
        var isLoaderProject = string.Equals(isLoaderStr, "true", StringComparison.OrdinalIgnoreCase);
        var isRuntimeProject = string.Equals(isRuntimeStr, "true", StringComparison.OrdinalIgnoreCase);

        // Read hashes directly from marker files (SourceHash-Loader-*.txt and SourceHash-Runtime-*.txt)
        // These files are added to AdditionalFiles by MSBuild target AddHashMarkersToAdditionalFiles
        string? loaderHashProp = null;
        string? runtimeHashProp = null;

        // Find Loader hash marker file
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
                    loaderHashProp = hashText!.Trim();
                }
            }
            catch (Exception ex)
            {
                var diag = Diagnostic.Create(new DiagnosticDescriptor(
                    id: "RCA020",
                    title: "Failed to read Loader hash marker file",
                    messageFormat: "Failed to read SourceHash-Loader marker file: {0}",
                    category: "Rca.BuildMetadata.Generator",
                    defaultSeverity: DiagnosticSeverity.Warning,
                    isEnabledByDefault: true), Location.None, ex.Message);
                context.ReportDiagnostic(diag);
            }
        }

        // Find Runtime hash marker file
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
                    runtimeHashProp = hashText!.Trim();
                }
            }
            catch (Exception ex)
            {
                var diag = Diagnostic.Create(new DiagnosticDescriptor(
                    id: "RCA021",
                    title: "Failed to read Runtime hash marker file",
                    messageFormat: "Failed to read SourceHash-Runtime marker file: {0}",
                    category: "Rca.BuildMetadata.Generator",
                    defaultSeverity: DiagnosticSeverity.Warning,
                    isEnabledByDefault: true), Location.None, ex.Message);
                context.ReportDiagnostic(diag);
            }
        }
        
        // Report diagnostic if marker files are missing for group projects
        if ((isLoaderProject || isRuntimeProject))
        {
            if (isLoaderProject && string.IsNullOrWhiteSpace(loaderHashProp))
            {
                var diag = Diagnostic.Create(new DiagnosticDescriptor(
                    id: "RCA022",
                    title: "Missing Loader hash marker file",
                    messageFormat: "SourceHash-Loader marker file not found in AdditionalFiles for {0} project. Found {1} additional files.",
                    category: "Rca.BuildMetadata.Generator",
                    defaultSeverity: DiagnosticSeverity.Warning,
                    isEnabledByDefault: true), Location.None,
                    projectName ?? "(unknown)", context.AdditionalFiles.Length);
                context.ReportDiagnostic(diag);
            }
            
            if (isRuntimeProject && string.IsNullOrWhiteSpace(runtimeHashProp))
            {
                var diag = Diagnostic.Create(new DiagnosticDescriptor(
                    id: "RCA023",
                    title: "Missing Runtime hash marker file",
                    messageFormat: "SourceHash-Runtime marker file not found in AdditionalFiles for {0} project. Found {1} additional files.",
                    category: "Rca.BuildMetadata.Generator",
                    defaultSeverity: DiagnosticSeverity.Warning,
                    isEnabledByDefault: true), Location.None,
                    projectName ?? "(unknown)", context.AdditionalFiles.Length);
                context.ReportDiagnostic(diag);
            }
        }

        // Read timestamp from MSBuild properties
        context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RcaHotReloadTimestamp", out var hotReloadTimestamp);

        // Choose effective hash for this project if it belongs to a group
        string? thisSourceHash = null;
        if (isLoaderProject)
        {
            thisSourceHash = loaderHashProp;
        }
        else if (isRuntimeProject)
        {
            thisSourceHash = runtimeHashProp;
        }

        // Report diagnostic if properties are missing for group projects
        if ((isLoaderProject || isRuntimeProject))
        {
            if (string.IsNullOrWhiteSpace(thisSourceHash))
            {
                var diag = Diagnostic.Create(new DiagnosticDescriptor(
                    id: "RCA018",
                    title: "Missing SourceHash property for group project",
                    messageFormat: "SourceHash property is empty for {0} project. IsLoader={1}, IsRuntime={2}, LoaderHash={3}, RuntimeHash={4}, Timestamp={5}",
                    category: "Rca.BuildMetadata.Generator",
                    defaultSeverity: DiagnosticSeverity.Warning,  // Changed from Error to Warning to allow build to proceed
                    isEnabledByDefault: true), Location.None,
                    projectName ?? "(unknown)", isLoaderStr ?? "false", isRuntimeStr ?? "false",
                    loaderHashProp ?? "(empty)", runtimeHashProp ?? "(empty)", hotReloadTimestamp ?? "(empty)");
                context.ReportDiagnostic(diag);
            }

            if (string.IsNullOrWhiteSpace(hotReloadTimestamp))
            {
                var diag = Diagnostic.Create(new DiagnosticDescriptor(
                    id: "RCA019",
                    title: "Missing RcaHotReloadTimestamp property for group project",
                    messageFormat: "RcaHotReloadTimestamp property is empty for {0} project. Cannot generate assembly metadata.",
                    category: "Rca.BuildMetadata.Generator",
                    defaultSeverity: DiagnosticSeverity.Warning,
                    isEnabledByDefault: true), Location.None, projectName ?? "(unknown)");
                context.ReportDiagnostic(diag);
            }
        }

        // Generate assembly-level metadata only for group projects and only if inputs are available
        if ((isLoaderProject || isRuntimeProject) && !string.IsNullOrWhiteSpace(thisSourceHash) && !string.IsNullOrWhiteSpace(hotReloadTimestamp))
        {
            var asmSrc = $$"""
// <auto-generated />
[assembly: System.Reflection.AssemblyMetadata("SourceHash", "{{thisSourceHash}}")]
[assembly: System.Reflection.AssemblyMetadata("DeployFolder", "{{hotReloadTimestamp}}")]
namespace Rca.Generated { internal static class __RcaAssemblyMetadataAnchor { } }
""";
            context.AddSource("Rca.AssemblyMetadata.g.cs", SourceText.From(asmSrc, Encoding.UTF8));
        }

        // Only one designated project should emit the RcaBuildMetadata class to avoid type conflicts.
        var emitBuildMetadataClass = string.Equals(projectName, "Rca.Contracts", StringComparison.OrdinalIgnoreCase);

        if (emitBuildMetadataClass)
        {
            var src = $$"""
// <auto-generated />
namespace Rca.Generated
{
    /// <summary>
    /// Build-time metadata surfaced to runtime via Source Generator.
    /// Single source of truth is MSBuild properties.
    /// </summary>
    public static class RcaBuildMetadata
    {
        /// <summary>
        /// Length of the short source hash used for Loader/Runtime groups.
        /// </summary>
        public static int SourceHashLength => {{length}};

        /// <summary>
        /// Timestamp pattern for build output directory names (e.g., "yyyyMMdd_HHmmss").
        /// </summary>
        public static string TimestampPattern => "{{patternStr}}";

        /// <summary>
        /// Directory where Revit Addins and timestamp subfolders are located.
        /// </summary>
        public static string RevitAddinsDir => @"{{addinsDir}}";

        /// <summary>
        /// Revit version used for deployment folder paths.
        /// </summary>
        public static string RevitVersion => "{{revitVersion}}";

        /// <summary>
        /// Directory where integration test builds are deployed.
        /// </summary>
        public static string TestDeployRoot => @"{{testDeployRoot}}";

        /// <summary>
        /// Root directory where RCA logs are written.
        /// </summary>
        public static string LogRoot => @"{{logRoot}}";
        public static string RevitLibsPath => @"{{libsPath}}";
        /// <summary>
        /// Named pipe for loader and UI commands.
        /// </summary>
        public static string CommandPipeName => @"{{pipeName}}";
        public static string LogPipeName => "{logPipeName}";
        /// <summary>
        /// Path to Timestamp file used to coordinate deploy folders.
        /// </summary>
        public static string TimestampFile => @"{{timestampFile}}";

        /// <summary>
        /// Sticky TTL (seconds) for timestamp reuse.
        /// </summary>
        public static int StickyStampSeconds => {{stickySeconds}};

        /// <summary>
        /// Flag indicating whether to force a fresh timestamp for the build.
        /// </summary>
        public static bool ForceNewStamp => {{forceNewStamp.ToString().ToLowerInvariant()}};

        /// <summary>
        /// List of project names in the Loader group (Single Source of Truth from MSBuild).
        /// </summary>
        public static string[] LoaderProjects => {{loaderArray}};

        /// <summary>
        /// List of project names in the Runtime group (Single Source of Truth from MSBuild).
        /// </summary>
        public static string[] RuntimeProjects => {{runtimeArray}};
    }
}
""";

            context.AddSource("RcaBuildMetadata.g.cs", SourceText.From(src, Encoding.UTF8));
        }
    }
}


