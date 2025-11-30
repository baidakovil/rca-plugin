namespace Rca.Tools.MetricsReporter.Processing;

using System;

/// <summary>
/// Normalizes symbol names (methods, types) from different metric sources to a unified format.
/// </summary>
/// <remarks>
/// This service handles the discrepancy between different metric sources:
/// - AltCover uses fully qualified type names: <c>Method(System.Object, Autodesk.Revit.UI.Events.IdlingEventArgs)</c>
/// - Roslyn uses short type names with nullable annotations: <c>Method(object? sender, IdlingEventArgs)</c>
/// 
/// Both are normalized to: <c>Method(...)</c> to ensure symbols from different sources are properly merged.
/// </remarks>
public static class SymbolNormalizer
{
  private const string ParameterPlaceholder = "...";

  /// <summary>
  /// Normalizes a method signature by removing parameter details and replacing them with a placeholder.
  /// </summary>
  /// <param name="methodSignature">The method signature to normalize (e.g., "Method(System.Object, System.String)" or "Method(object? sender, string name)").</param>
  /// <returns>
  /// Normalized method signature with parameters replaced by <c>...</c> (e.g., "Method(...)").
  /// If the input is <see langword="null"/> or whitespace, returns the input unchanged.
  /// </returns>
  /// <remarks>
  /// This method handles various signature formats:
  /// - AltCover format: <c>Namespace.Type.Method(System.Object, Autodesk.Revit.UI.Events.IdlingEventArgs)</c>
  /// - Roslyn format: <c>Namespace.Type.Method(object? sender, IdlingEventArgs e)</c>
  /// - Both are normalized to: <c>Namespace.Type.Method(...)</c>
  /// 
  /// The normalization process:
  /// 1. Finds the opening parenthesis of the parameter list
  /// 2. Replaces everything from the opening parenthesis to the matching closing parenthesis with "..."
  /// 3. Handles nested parentheses in generic types (e.g., <c>Method(List&lt;string&gt; items)</c>)
  /// </remarks>
  public static string? NormalizeMethodSignature(string? methodSignature)
  {
    if (string.IsNullOrWhiteSpace(methodSignature))
    {
      return methodSignature;
    }

    var paramStart = methodSignature.IndexOf('(');
    if (paramStart < 0)
    {
      // No parameters, return as-is
      return methodSignature;
    }

    // Find the matching closing parenthesis, handling nested parentheses
    var paramEnd = FindMatchingClosingParenthesis(methodSignature, paramStart);
    if (paramEnd < 0)
    {
      // Malformed signature, return as-is
      return methodSignature;
    }

    // Replace parameters with placeholder
    return methodSignature[..(paramStart + 1)] + ParameterPlaceholder + methodSignature[paramEnd..];
  }

  /// <summary>
  /// Extracts the method name without parameters from a full method signature.
  /// </summary>
  /// <param name="methodSignature">The method signature (e.g., "Method(System.Object)" or "Method(object? sender)").</param>
  /// <returns>
  /// The method name without parameters (e.g., "Method").
  /// If the input is <see langword="null"/> or whitespace, returns the input unchanged.
  /// </returns>
  /// <remarks>
  /// This method extracts just the method name part, removing:
  /// - Return type prefix (e.g., "void Method(...)")
  /// - Parameter list (e.g., "Method(System.Object)")
  /// - Generic type parameters (e.g., "Method&lt;T&gt;(...)")
  /// </remarks>
  public static string? ExtractMethodName(string? methodSignature)
  {
    if (string.IsNullOrWhiteSpace(methodSignature))
    {
      return methodSignature;
    }

    // Remove return type if present (format: "ReturnType Method(...)")
    var spaceIndex = methodSignature.IndexOf(' ');
    var nameStart = spaceIndex >= 0 ? spaceIndex + 1 : 0;

    // Find parameter list start and constraints
    var paramStart = methodSignature.IndexOf('(', nameStart);
    var whereIndex = methodSignature.IndexOf(" where ", StringComparison.Ordinal);

    // Determine where the method name part ends (before parameters or constraints)
    var methodNameEnd = methodSignature.Length;
    if (paramStart >= nameStart)
    {
      methodNameEnd = paramStart;
    }
    if (whereIndex >= nameStart && whereIndex < methodNameEnd)
    {
      methodNameEnd = whereIndex;
    }

    // Find generic parameters start in the original signature (before parameters/constraints)
    // This is more reliable than searching in the extracted part
    // Note: We need to distinguish between actual generic parameters (Method<T>) and
    // method names that contain angle brackets (like <Clone>$)
    var genericStartInSignature = methodSignature.IndexOf('<', nameStart);
    string methodNameWithoutGenerics;

    if (genericStartInSignature >= 0 && genericStartInSignature < methodNameEnd)
    {
      // Check if this is actually a generic parameter list by finding the matching '>'
      // and verifying it's followed by valid generic parameter continuation (space, '(', or end)
      var genericEnd = FindMatchingClosingAngleBracket(methodSignature, genericStartInSignature);
      if (genericEnd >= 0 && genericEnd < methodNameEnd)
      {
        // Check if after '>' there's a space, '(', ')' or end of method name part
        // This indicates it's a generic parameter list (Method<T>(...) or Method<T> where ...)
        var afterGeneric = genericEnd + 1;
        if (afterGeneric >= methodNameEnd ||
            methodSignature[afterGeneric] == ' ' ||
            methodSignature[afterGeneric] == '(' ||
            methodSignature[afterGeneric] == ')')
        {
          // It's a generic parameter list - extract only the part before generics
          methodNameWithoutGenerics = methodSignature[nameStart..genericStartInSignature].Trim();
        }
        else
        {
          // '<' is part of the method name (like <Clone>$), not a generic parameter
          methodNameWithoutGenerics = methodSignature[nameStart..methodNameEnd].Trim();
        }
      }
      else
      {
        // No matching '>', so '<' is part of the method name
        methodNameWithoutGenerics = methodSignature[nameStart..methodNameEnd].Trim();
      }
    }
    else
    {
      // No generics - extract the part before parameters/constraints
      methodNameWithoutGenerics = methodSignature[nameStart..methodNameEnd].Trim();
    }

    // Extract just the method name (after the last dot, if any)
    // This handles fully qualified names like "Namespace.Type.Method"
    var lastDot = methodNameWithoutGenerics.LastIndexOf('.');
    var extractedName = lastDot >= 0 ? methodNameWithoutGenerics[(lastDot + 1)..].Trim() : methodNameWithoutGenerics.Trim();

    // Special handling for constructors: if the extracted name is "ctor" or "cctor",
    // check if the original had a dot before it (e.g., ".ctor" or ".cctor")
    if (extractedName == "ctor" || extractedName == "cctor")
    {
      // Check if there's a dot before "ctor" or "cctor" in the original string
      // Look for pattern like "..ctor" or "..cctor" (double dot indicates .ctor/.cctor)
      var beforeLastDot = lastDot > 0 ? methodNameWithoutGenerics[..lastDot] : string.Empty;
      if (beforeLastDot.EndsWith('.'))
      {
        // It's a constructor, add the leading dot
        extractedName = "." + extractedName;
      }
    }

    return extractedName;
  }

  /// <summary>
  /// Normalizes a fully qualified method name by normalizing the method signature part.
  /// </summary>
  /// <param name="fullyQualifiedMethodName">The fully qualified method name (e.g., "Namespace.Type.Method(System.Object)").</param>
  /// <returns>
  /// Normalized fully qualified method name (e.g., "Namespace.Type.Method(...)").
  /// If the input is <see langword="null"/> or whitespace, returns the input unchanged.
  /// </returns>
  /// <remarks>
  /// This method preserves the namespace and type parts while normalizing only the method signature.
  /// It handles both AltCover and Roslyn formats by applying signature normalization to the method part.
  /// 
  /// The method works by:
  /// 1. Finding the parameter list (opening parenthesis)
  /// 2. Finding the matching closing parenthesis (handling nested parentheses in generic types)
  /// 3. Replacing the entire parameter list with "..."
  /// 
  /// This approach is simpler and more reliable than trying to parse the method name separately.
  /// </remarks>
  public static string? NormalizeFullyQualifiedMethodName(string? fullyQualifiedMethodName)
  {
    if (string.IsNullOrWhiteSpace(fullyQualifiedMethodName))
    {
      return fullyQualifiedMethodName;
    }

    // First, remove generic type parameters from the method name (e.g., "Process<T>" -> "Process")
    // This ensures methods with different generic parameters are treated as the same method for aggregation
    // Note: We need to distinguish between actual generic parameters (Method<T>) and
    // method names that contain angle brackets (like <Clone>$)
    var paramStart = fullyQualifiedMethodName.IndexOf('(');
    var searchEnd = paramStart >= 0 ? paramStart : fullyQualifiedMethodName.Length;

    // Find generic parameters before the method parameter list
    var genericStart = fullyQualifiedMethodName.IndexOf('<');
    if (genericStart >= 0 && genericStart < searchEnd)
    {
      var genericEnd = FindMatchingClosingAngleBracket(fullyQualifiedMethodName, genericStart);
      if (genericEnd >= 0 && genericEnd < searchEnd)
      {
        // Check if after '>' there's a space, '(', ')' or end of method name part
        // This indicates it's a generic parameter list (Method<T>(...) or Method<T> where ...)
        var afterGeneric = genericEnd + 1;
        if (afterGeneric >= searchEnd ||
            fullyQualifiedMethodName[afterGeneric] == ' ' ||
            fullyQualifiedMethodName[afterGeneric] == '(' ||
            fullyQualifiedMethodName[afterGeneric] == ')')
        {
          // It's a generic parameter list - remove it: "Method<T>(...)" -> "Method(...)"
          fullyQualifiedMethodName = fullyQualifiedMethodName[..genericStart] + fullyQualifiedMethodName[(genericEnd + 1)..];
        }
        // Otherwise, '<' is part of the method name (like <Clone>$), so don't remove it
      }
    }

    // Then apply method signature normalization which will find and replace parameters
    return NormalizeMethodSignature(fullyQualifiedMethodName);
  }

  /// <summary>
  /// Normalizes a type name by removing generic type parameters.
  /// </summary>
  /// <param name="typeName">The type name to normalize (e.g., "List&lt;string&gt;" or "Dictionary&lt;string, int&gt;").</param>
  /// <returns>
  /// Normalized type name without generic parameters (e.g., "List").
  /// If the input is <see langword="null"/> or whitespace, returns the input unchanged.
  /// </returns>
  /// <remarks>
  /// This method removes generic type parameters to ensure types with different generic arguments
  /// are treated as the same base type for aggregation purposes.
  /// </remarks>
  public static string? NormalizeTypeName(string? typeName)
  {
    if (string.IsNullOrWhiteSpace(typeName))
    {
      return typeName;
    }

    var genericStart = typeName.IndexOf('<');
    if (genericStart < 0)
    {
      return typeName;
    }

    return typeName[..genericStart].Trim();
  }

  private static int FindMatchingClosingParenthesis(string text, int openIndex)
  {
    var depth = 0;
    for (var i = openIndex; i < text.Length; i++)
    {
      var ch = text[i];
      if (ch == '(')
      {
        depth++;
      }
      else if (ch == ')')
      {
        depth--;
        if (depth == 0)
        {
          return i;
        }
      }
    }

    return -1;
  }

  private static int FindMatchingClosingAngleBracket(string text, int openIndex)
  {
    var depth = 0;
    for (var i = openIndex; i < text.Length; i++)
    {
      var ch = text[i];
      if (ch == '<')
      {
        depth++;
      }
      else if (ch == '>')
      {
        depth--;
        if (depth == 0)
        {
          return i;
        }
      }
    }

    return -1;
  }
}

