namespace Rca.BuildMetadata.Generator;

/// <summary>
/// Data structure containing all MSBuild properties read by the Source Generator.
/// This serves as a data transfer object between property reading and code generation phases.
/// Instances are created by <see cref="BuildPropertyReader"/> and consumed by code generation emitters.
/// </summary>
internal sealed class BuildProperties
{
    /// <summary>
    /// Length of the short source hash used for Loader/Runtime groups.
    /// </summary>
    public int SourceHashLength { get; set; }

    /// <summary>
    /// Timestamp pattern for build output directory names (e.g., "yyyyMMdd_HHmmss").
    /// </summary>
    public string TimestampPattern { get; set; } = string.Empty;

    /// <summary>
    /// Directory where Revit Addins and timestamp subfolders are located.
    /// </summary>
    public string RevitAddinsDir { get; set; } = string.Empty;

    /// <summary>
    /// Directory where integration test builds are deployed.
    /// </summary>
    public string TestDeployRoot { get; set; } = string.Empty;

    /// <summary>
    /// Root directory where RCA logs are written.
    /// </summary>
    public string LogRoot { get; set; } = string.Empty;

    /// <summary>
    /// Revit version used for deployment folder paths.
    /// </summary>
    public string RevitVersion { get; set; } = string.Empty;

    /// <summary>
    /// Path to Revit libraries directory.
    /// </summary>
    public string RevitLibsPath { get; set; } = string.Empty;

    /// <summary>
    /// Named pipe for loader and UI commands.
    /// </summary>
    public string? CommandPipeName { get; set; }

    /// <summary>
    /// Named pipe for UI logging transport.
    /// </summary>
    public string? LogPipeName { get; set; }

    /// <summary>
    /// Path to timestamp file used to coordinate deploy folders.
    /// </summary>
    public string TimestampFile { get; set; } = string.Empty;

    /// <summary>
    /// Sticky TTL (seconds) for timestamp reuse.
    /// </summary>
    public int StickyStampSeconds { get; set; }

    /// <summary>
    /// Flag indicating whether to force a fresh timestamp for the build.
    /// </summary>
    public bool ForceNewStamp { get; set; }

    /// <summary>
    /// Semicolon-separated list of project names in the Loader group.
    /// </summary>
    public string? LoaderProjectsList { get; set; }

    /// <summary>
    /// Semicolon-separated list of project names in the Runtime group.
    /// </summary>
    public string? RuntimeProjectsList { get; set; }

    /// <summary>
    /// MSBuild project name being compiled.
    /// </summary>
    public string? ProjectName { get; set; }

    /// <summary>
    /// Hot-reload timestamp value from MSBuild (may be empty during parallel builds).
    /// </summary>
    public string? HotReloadTimestamp { get; set; }
}

