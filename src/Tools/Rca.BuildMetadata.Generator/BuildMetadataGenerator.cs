using System;
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
    }
}
""";

        context.AddSource("RcaBuildMetadata.g.cs", SourceText.From(src, Encoding.UTF8));
    }
}


