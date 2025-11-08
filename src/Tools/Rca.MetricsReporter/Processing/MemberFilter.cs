namespace Rca.Tools.MetricsReporter.Processing;

using System;
using System.Collections.Generic;

/// <summary>
/// Filters out compiler-generated and constructor methods from metrics reports.
/// </summary>
/// <remarks>
/// This filter excludes methods that are not relevant for code quality metrics:
/// - Constructors (.ctor, .cctor) - they are typically boilerplate and don't represent meaningful code complexity
/// - Compiler-generated methods (MoveNext, SetStateMachine, MoveNextAsync, DisposeAsync) - these are generated
///   by the compiler for async/await state machines and enumerators, and don't represent actual user code
/// 
/// The list of excluded methods is intentionally hardcoded as a design decision to keep the filtering
/// simple and explicit.
/// </remarks>
public static class MemberFilter
{
    /// <summary>
    /// Gets the set of method names that should be excluded from metrics reports.
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
    private static readonly HashSet<string> ExcludedMethodNames = new(StringComparer.Ordinal)
    {
        "ctor",
        "cctor",
        "MoveNext",
        "SetStateMachine",
        "MoveNextAsync",
        "DisposeAsync"
    };

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
    public static bool ShouldExcludeMethod(string? methodName)
    {
        if (string.IsNullOrWhiteSpace(methodName))
        {
            return false;
        }

        // Handle constructor names with leading dot (e.g., ".ctor" -> "ctor")
        var normalizedName = methodName.StartsWith(".", StringComparison.Ordinal)
            ? methodName[1..]
            : methodName;

        return ExcludedMethodNames.Contains(normalizedName);
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
    public static bool ShouldExcludeMethodByFqn(string? fullyQualifiedMethodName)
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
