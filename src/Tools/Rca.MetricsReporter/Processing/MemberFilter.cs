namespace Rca.Tools.MetricsReporter.Processing;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Filters out compiler-generated and constructor methods from metrics reports.
/// </summary>
/// <remarks>
/// This filter excludes methods that are not relevant for code quality metrics:
/// - Constructors (.ctor, .cctor) - they are typically boilerplate and don't represent meaningful code complexity
/// - Compiler-generated methods (MoveNext, SetStateMachine, MoveNextAsync, DisposeAsync) - these are generated
///   by the compiler for async/await state machines and enumerators, and don't represent actual user code
/// 
/// The list of excluded methods can be configured via MSBuild property ExcludedMethodNames.
/// Default values are provided if no configuration is supplied.
/// </remarks>
public sealed class MemberFilter
{
    /// <summary>
    /// Gets the default set of method names that should be excluded from metrics reports.
    /// </summary>
    /// <remarks>
    /// This set contains:
    /// - "ctor" - instance constructors (normalized from ".ctor")
    /// - "cctor" - static constructors (normalized from ".cctor")
    /// - "MoveNext" - compiler-generated method for IEnumerator
    /// - "SetStateMachine" - compiler-generated method for async state machines
    /// - "MoveNextAsync" - compiler-generated method for async enumerators
    /// - "DisposeAsync" - compiler-generated method for async disposal
    /// </remarks>
    private static readonly HashSet<string> DefaultExcludedMethodNames = new(StringComparer.Ordinal)
    {
        "ctor",
        "cctor",
        "MoveNext",
        "SetStateMachine",
        "MoveNextAsync",
        "DisposeAsync"
    };

    private readonly HashSet<string> _excludedMethodNames;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemberFilter"/> class with default excluded method names.
    /// </summary>
    public MemberFilter()
        : this(DefaultExcludedMethodNames)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemberFilter"/> class with the specified excluded method names.
    /// </summary>
    /// <param name="excludedMethodNames">The set of method names to exclude. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="excludedMethodNames"/> is null.</exception>
    public MemberFilter(HashSet<string> excludedMethodNames)
    {
        ArgumentNullException.ThrowIfNull(excludedMethodNames);
        _excludedMethodNames = new HashSet<string>(excludedMethodNames, StringComparer.Ordinal);
    }

    /// <summary>
    /// Determines whether a method should be excluded from metrics reports.
    /// </summary>
    /// <param name="methodName">The normalized method name (e.g., "ctor", "MoveNext", "DoWork").</param>
    /// <returns>
    /// <see langword="true"/> if the method should be excluded from the report; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This method checks if the method name is in the excluded set. The method name should be
    /// normalized (e.g., ".ctor" should be passed as "ctor" without the leading dot).
    /// </remarks>
    public bool ShouldExcludeMethod(string? methodName)
    {
        if (string.IsNullOrWhiteSpace(methodName))
        {
            return false;
        }

        // Handle constructor names with leading dot (e.g., ".ctor" -> "ctor")
        var normalizedName = methodName.StartsWith(".", StringComparison.Ordinal)
            ? methodName[1..]
            : methodName;

        return _excludedMethodNames.Contains(normalizedName);
    }

    /// <summary>
    /// Determines whether a method should be excluded from metrics reports based on its fully qualified name.
    /// </summary>
    /// <param name="fullyQualifiedMethodName">
    /// The fully qualified method name (e.g., "Namespace.Type.Method(...)", "Namespace.Type..ctor(...)").
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the method should be excluded from the report; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This method extracts the method name from the fully qualified name and checks if it should be excluded.
    /// It handles normalized FQN format where parameters are replaced with "...".
    /// It also handles Roslyn-style constructors where the method name matches the type name.
    /// </remarks>
    public bool ShouldExcludeMethodByFqn(string? fullyQualifiedMethodName)
    {
        if (string.IsNullOrWhiteSpace(fullyQualifiedMethodName))
        {
            return false;
        }

        // Extract method name from FQN
        // Format: "Namespace.Type.Method(...)" or "Namespace.Type..ctor(...)"
        var methodName = SymbolNormalizer.ExtractMethodName(fullyQualifiedMethodName);
        
        // Check if method name is in the excluded set
        if (ShouldExcludeMethod(methodName))
        {
            return true;
        }

        // Check if this is a Roslyn-style constructor (method name matches type name)
        // Format: "Namespace.Type.Type(...)" where the last "Type" before "(" is the method name
        // This happens when Roslyn represents constructors as "TypeName.TypeName(...)"
        var typeName = ExtractTypeNameFromFqn(fullyQualifiedMethodName);
        if (!string.IsNullOrWhiteSpace(typeName) && !string.IsNullOrWhiteSpace(methodName))
        {
            // In Roslyn format, constructors have the pattern "TypeName.TypeName(...)"
            // where the method name (after the last dot before parameters) matches the type name
            // So if methodName == typeName, it's a constructor and should be excluded
            if (string.Equals(methodName, typeName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Creates a <see cref="MemberFilter"/> instance from a comma-separated or semicolon-separated string of method names.
    /// </summary>
    /// <param name="excludedMethodNamesString">
    /// A string containing method names separated by commas or semicolons (e.g., "ctor,cctor,MoveNext" or "ctor;cctor;MoveNext").
    /// Whitespace around names is trimmed. Empty or null string returns a filter with default excluded methods.
    /// </param>
    /// <returns>
    /// A <see cref="MemberFilter"/> instance configured with the specified method names, or default excluded methods if the string is empty or null.
    /// </returns>
    /// <remarks>
    /// This method is useful for parsing method names from configuration files or command-line arguments.
    /// Method names are normalized (leading dots are removed, e.g., ".ctor" becomes "ctor").
    /// </remarks>
    public static MemberFilter FromString(string? excludedMethodNamesString)
    {
        if (string.IsNullOrWhiteSpace(excludedMethodNamesString))
        {
            return new MemberFilter();
        }

        var methodNames = new HashSet<string>(StringComparer.Ordinal);
        var separators = new[] { ',', ';' };
        var parts = excludedMethodNamesString.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                continue;
            }

            // Normalize method name (remove leading dot if present)
            var normalizedName = part.StartsWith(".", StringComparison.Ordinal) ? part[1..] : part;
            if (!string.IsNullOrWhiteSpace(normalizedName))
            {
                methodNames.Add(normalizedName);
            }
        }

        return methodNames.Count == 0 ? new MemberFilter() : new MemberFilter(methodNames);
    }

    /// <summary>
    /// Gets a comma-separated string of excluded method names.
    /// </summary>
    /// <returns>
    /// A comma-separated string of excluded method names, or an empty string if no methods are excluded.
    /// </returns>
    /// <remarks>
    /// This method returns the list of excluded method names in a format suitable for display.
    /// The names are sorted alphabetically for consistent output.
    /// </remarks>
    public string GetExcludedMethodNamesString()
    {
        if (_excludedMethodNames.Count == 0)
        {
            return string.Empty;
        }

        var sortedNames = _excludedMethodNames.OrderBy(x => x, StringComparer.Ordinal);
        return string.Join(", ", sortedNames);
    }

    /// <summary>
    /// Extracts the type name from a fully qualified method name.
    /// </summary>
    /// <param name="fullyQualifiedMethodName">
    /// The fully qualified method name (e.g., "Namespace.Type.Method(...)").
    /// </param>
    /// <returns>
    /// The type name (e.g., "Type") or <see langword="null"/> if extraction fails.
    /// </returns>
    /// <remarks>
    /// This method extracts the last part of the namespace/type path before the method name.
    /// For "Namespace.Type.Method(...)", it returns "Type".
    /// </remarks>
    private static string? ExtractTypeNameFromFqn(string fullyQualifiedMethodName)
    {
        if (string.IsNullOrWhiteSpace(fullyQualifiedMethodName))
        {
            return null;
        }

        // Find the parameter list start
        var paramStart = fullyQualifiedMethodName.IndexOf('(');
        var searchEnd = paramStart >= 0 ? paramStart : fullyQualifiedMethodName.Length;

        // Find the last dot before the method name (before parameters)
        var lastDot = fullyQualifiedMethodName.LastIndexOf('.', searchEnd - 1);
        if (lastDot < 0)
        {
            return null;
        }

        // Extract the part before the last dot (this is the type FQN)
        var typeFqn = fullyQualifiedMethodName[..lastDot];
        
        // Extract the type name (the last part after the last dot in the type FQN)
        var typeNameLastDot = typeFqn.LastIndexOf('.');
        return typeNameLastDot >= 0 ? typeFqn[(typeNameLastDot + 1)..] : typeFqn;
    }
}
