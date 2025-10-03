using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Rca.Logging.Contracts;

/// <summary>
/// Represents a single structured log entry transferred from Runtime to Loader over the dedicated logging named pipe.
/// WHY: A minimal immutable DTO to avoid unnecessary dependencies and keep a stable contract between hot-reloadable runtime
/// and the stable loader domain. Fields use primitive types for simpler and faster JSON serialization.
/// Converted to a record to allow non-destructive copying with 'with' for fallback enrichment.
/// </summary>
public sealed record LogEntryDto
{
    /// <summary>
    /// Fixed schema version string. Loader can ignore unknown versions in future evolutions.
    /// </summary>
    public string SchemaVersion { get; init; } = LoggingSchema.Version;

    /// <summary>
    /// Local time ticks (DateTime.Now.Ticks) captured at log creation to avoid time skew after transport delays.
    /// </summary>
    public long TimestampTicks { get; init; }

    /// <summary>
    /// Log level textual representation (Trace, Debug, Information, Warning, Error, Critical).
    /// Stored as string to keep DTO decoupled from Microsoft.Extensions.Logging abstractions.
    /// </summary>
    public string Level { get; init; } = string.Empty;

    /// <summary>
    /// Logger category (usually the fully qualified class name) or logical grouping.
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Rendered message after template formatting performed by the logger implementation.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Optional flattened exception string (Type:Message + stack trace). Null if no exception.
    /// </summary>
    public string? Exception { get; init; }

    /// <summary>
    /// Optional set of scope key-value pairs captured at logging time. Values limited to primitives for safety.
    /// </summary>
    public Dictionary<string, object?>? Scope { get; init; }

    /// <summary>
    /// Identifier of the logical runtime session (regenerated on each hot reload of the AssemblyLoadContext instance).
    /// </summary>
    public string RuntimeSessionId { get; init; } = string.Empty;

    /// <summary>
    /// Per-runtime strictly monotonic sequence id produced via Interlocked.Increment for ordering.
    /// </summary>
    public long SequenceId { get; init; }

    /// <summary>
    /// OS process id of the runtime process (identical to Loader process but stored explicitly for completeness/diagnostics).
    /// </summary>
    public int RuntimeProcessId { get; init; }

    /// <summary>
    /// Optional AssemblyLoadContext instance id (if tracked). Null when not applicable.
    /// </summary>
    public int? ALCInstanceId { get; init; }

    /// <summary>
    /// True when written through fallback file mechanism instead of pipe (transport unavailable at the moment of logging).
    /// </summary>
    public bool IsFallback { get; init; }

    /// <summary>
    /// Bit flags for special conditions (see <see cref="LoggingFlags"/>).
    /// </summary>
    public int Flags { get; init; }
}

/// <summary>
/// Central location for schema version constants.
/// </summary>
public static class LoggingSchema
{
    /// <summary>
    /// Current log DTO schema version. Increment only with backward incompatible changes.
    /// </summary>
    public const string Version = "1";
}

/// <summary>
/// Bit flags describing special serialization / transport states of a log entry.
/// WHY: Using bit flags keeps DTO compact and allows combining states without introducing additional booleans.
/// </summary>
public static class LoggingFlags
{
    /// <summary>
    /// Set when JSON serialization of the original entry failed and the entry was redirected to the emergency file. Not transmitted.
    /// </summary>
    public const int SerializationFailed = 1 << 0;

    /// <summary>
    /// Set when the entry was written using fallback path (local file) instead of pipe transport.
    /// </summary>
    public const int FallbackUsed = 1 << 1;
}
