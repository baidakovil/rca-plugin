namespace Rca.Tools.MetricsReporter.Processing;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Filters out types from metrics reports based on name patterns.
/// </summary>
/// <remarks>
/// This filter excludes types whose fully qualified names contain any of the specified exclusion patterns.
/// The filter supports multiple exclusion patterns separated by commas or semicolons.
/// Matching is case-sensitive and checks if the type name contains the pattern.
/// </remarks>
public sealed class TypeFilter
{
    private readonly HashSet<string> _excludedPatterns;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeFilter"/> class with no exclusions.
    /// </summary>
    public TypeFilter()
        : this(new HashSet<string>(StringComparer.Ordinal))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeFilter"/> class with the specified exclusion patterns.
    /// </summary>
    /// <param name="excludedPatterns">The set of patterns to exclude. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="excludedPatterns"/> is null.</exception>
    public TypeFilter(HashSet<string> excludedPatterns)
    {
        ArgumentNullException.ThrowIfNull(excludedPatterns);
        _excludedPatterns = new HashSet<string>(excludedPatterns, StringComparer.Ordinal);
    }

    /// <summary>
    /// Determines whether a type should be excluded from metrics reports.
    /// </summary>
    /// <param name="typeNameOrFqn">The type name or fully qualified name to check.</param>
    /// <returns>
    /// <see langword="true"/> if the type should be excluded from the report; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This method checks if the provided name contains any of the exclusion patterns.
    /// Matching is case-sensitive. Returns <see langword="false"/> if the name is null or empty.
    /// </remarks>
    public bool ShouldExcludeType(string? typeNameOrFqn)
    {
        if (string.IsNullOrWhiteSpace(typeNameOrFqn))
        {
            return false;
        }

        return _excludedPatterns.Any(pattern => typeNameOrFqn.Contains(pattern, StringComparison.Ordinal));
    }

    /// <summary>
    /// Creates a <see cref="TypeFilter"/> instance from a comma-separated or semicolon-separated string of exclusion patterns.
    /// </summary>
    /// <param name="excludedTypeNamePatterns">
    /// A string containing exclusion patterns separated by commas or semicolons (e.g., "&lt;&gt;c,__DisplayClass").
    /// Whitespace around patterns is trimmed. Empty or null string returns a filter with no exclusions.
    /// </param>
    /// <returns>
    /// A <see cref="TypeFilter"/> instance configured with the specified patterns, or an empty filter if the string is empty or null.
    /// </returns>
    /// <remarks>
    /// This method is useful for parsing exclusion patterns from configuration files or command-line arguments.
    /// Patterns are matched case-sensitively against fully qualified type names using substring matching.
    /// </remarks>
    public static TypeFilter FromString(string? excludedTypeNamePatterns)
    {
        if (string.IsNullOrWhiteSpace(excludedTypeNamePatterns))
        {
            return new TypeFilter();
        }

        var patterns = new HashSet<string>(StringComparer.Ordinal);
        var separators = new[] { ',', ';' };
        var parts = excludedTypeNamePatterns.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (!string.IsNullOrWhiteSpace(part))
            {
                patterns.Add(part);
            }
        }

        return new TypeFilter(patterns);
    }

    /// <summary>
    /// Gets a comma-separated string of excluded type name patterns.
    /// </summary>
    /// <returns>
    /// A comma-separated string of excluded type name patterns, or an empty string if no patterns are excluded.
    /// </returns>
    /// <remarks>
    /// This method returns the list of excluded type name patterns in a format suitable for display.
    /// The patterns are sorted alphabetically for consistent output.
    /// </remarks>
    public string GetExcludedTypePatternsString()
    {
        if (_excludedPatterns.Count == 0)
        {
            return string.Empty;
        }

        var sortedPatterns = _excludedPatterns.OrderBy(x => x, StringComparer.Ordinal);
        return string.Join(", ", sortedPatterns);
    }
}


