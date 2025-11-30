namespace Rca.Tools.MetricsReporter.Processing;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Performs a lightweight Roslyn-based scan of the solution source tree to locate
/// <see cref="System.Diagnostics.CodeAnalysis.SuppressMessageAttribute"/> usages.
/// </summary>
/// <remarks>
/// The analyzer works purely on syntax trees and simple path heuristics:
/// <list type="number">
/// <item>
/// <description>
/// Enumerates <c>.cs</c> files only under the specified source code folders
/// (relative to the solution directory).
/// </description>
/// </item>
/// <item>
/// <description>
/// Derives a logical assembly name by finding the longest matching source code folder
/// prefix in the file path and taking the next segment as the assembly name.
/// For example, with <c>SourceCodeFolders = ["src", "src/Tools"]</c> and file
/// <c>src/Tools/Rca.MetricsReporter/File.cs</c>, the analyzer matches <c>src/Tools</c>
/// (longest prefix) and derives assembly name <c>Rca.MetricsReporter</c>.
/// </description>
/// </item>
/// <item>
/// <description>
/// Applies <see cref="AssemblyFilter"/> rules to skip excluded assemblies before processing files.
/// </description>
/// </item>
/// <item>
/// <description>
/// Walks syntax trees to find suppression attributes, maps rule identifiers
/// (for example, <c>CA1506</c>) to <see cref="MetricIdentifier"/> values via
/// <see cref="SuppressedRuleMetricMapper"/>, and emits normalized fully qualified
/// names so that results can be correlated with metrics nodes.
/// </description>
/// </item>
/// </list>
/// This approach avoids the complexity of loading full Roslyn compilations while
/// still providing stable identifiers for HTML and downstream tooling.
/// </remarks>
internal static class SuppressedSymbolsAnalyzer
{
  /// <summary>
  /// Executes suppressed symbol analysis for the specified solution directory.
  /// </summary>
  /// <param name="solutionDirectory">Root directory of the solution source tree.</param>
  /// <param name="sourceCodeFolders">
  /// Collection of source code folder paths (relative to <paramref name="solutionDirectory"/>)
  /// that contain assembly projects. Only files under these folders are scanned.
  /// </param>
  /// <param name="excludedAssemblyNames">
  /// Comma- or semicolon-separated list of assembly patterns to exclude.
  /// </param>
  /// <param name="cancellationToken">Cancellation token for the operation.</param>
  /// <returns>A report containing all discovered suppressed symbols.</returns>
  public static SuppressedSymbolsReport Analyze(
      string solutionDirectory,
      IReadOnlyCollection<string> sourceCodeFolders,
      string? excludedAssemblyNames,
      CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(solutionDirectory))
    {
      throw new ArgumentException("Solution directory must be provided for suppressed symbol analysis.", nameof(solutionDirectory));
    }

    var normalizedRoot = Path.GetFullPath(solutionDirectory);
    var assemblyFilter = AssemblyFilter.FromString(excludedAssemblyNames);
    var suppressedSymbols = new List<SuppressedSymbolInfo>();

    // Normalize source code folders: sort by length (longest first) for longest-prefix matching
    var normalizedFolders = sourceCodeFolders
      .Where(f => !string.IsNullOrWhiteSpace(f))
      .Select(f => NormalizePath(f))
      .OrderByDescending(f => f.Length)
      .ToArray();

    if (normalizedFolders.Length == 0)
    {
      // If no source code folders specified, fall back to scanning everything
      // (backward compatibility, though not recommended)
      normalizedFolders = new[] { string.Empty };
    }

    foreach (var filePath in EnumerateCSharpFiles(normalizedRoot, normalizedFolders))
    {
      cancellationToken.ThrowIfCancellationRequested();

      var assemblyName = TryResolveAssemblyName(normalizedRoot, filePath, normalizedFolders);
      if (string.IsNullOrWhiteSpace(assemblyName) || assemblyFilter.ShouldExcludeAssembly(assemblyName))
      {
        continue;
      }

      var relativePath = Path.GetRelativePath(normalizedRoot, filePath);
      AnalyzeSingleFile(filePath, relativePath, suppressedSymbols, cancellationToken);
    }

    return new SuppressedSymbolsReport
    {
      GeneratedAtUtc = DateTime.UtcNow,
      SuppressedSymbols = suppressedSymbols
    };
  }

  private static string NormalizePath(string path)
  {
    // Normalize path separators and remove leading/trailing separators
    return path.Replace('\\', Path.DirectorySeparatorChar)
               .Replace('/', Path.DirectorySeparatorChar)
               .Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
  }

  private static IEnumerable<string> EnumerateCSharpFiles(string solutionDirectory, string[] sourceCodeFolders)
  {
    if (!Directory.Exists(solutionDirectory))
    {
      return Array.Empty<string>();
    }

    var allFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var folder in sourceCodeFolders)
    {
      var folderPath = string.IsNullOrEmpty(folder)
        ? solutionDirectory
        : Path.Combine(solutionDirectory, folder);

      if (!Directory.Exists(folderPath))
      {
        continue;
      }

      var files = Directory.EnumerateFiles(folderPath, "*.cs", SearchOption.AllDirectories);
      foreach (var file in files)
      {
        allFiles.Add(file);
      }
    }

    return allFiles;
  }

  private static string? TryResolveAssemblyName(
      string solutionDirectory,
      string filePath,
      string[] sourceCodeFolders)
  {
    var relative = Path.GetRelativePath(solutionDirectory, filePath);
    var normalizedRelative = NormalizePath(relative);
    var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

    // Find the longest matching source code folder prefix
    string? matchedPrefix = null;
    foreach (var folder in sourceCodeFolders)
    {
      if (string.IsNullOrEmpty(folder))
      {
        continue;
      }

      var normalizedFolder = NormalizePath(folder);
      if (normalizedRelative.StartsWith(normalizedFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
          normalizedRelative.Equals(normalizedFolder, StringComparison.OrdinalIgnoreCase))
      {
        // Prefer longer matches (folders are already sorted by length descending)
        if (matchedPrefix is null || normalizedFolder.Length > matchedPrefix.Length)
        {
          matchedPrefix = normalizedFolder;
        }
      }
    }

    if (matchedPrefix is null)
    {
      // No source code folder matched - treat first segment as assembly name
      var segments = normalizedRelative.Split(separators, StringSplitOptions.RemoveEmptyEntries);
      return segments.Length > 0 ? segments[0] : null;
    }

    // Remove the matched prefix and take the next segment as assembly name
    var remaining = normalizedRelative.Substring(matchedPrefix.Length).Trim(separators);
    var remainingSegments = remaining.Split(separators, StringSplitOptions.RemoveEmptyEntries);
    return remainingSegments.Length > 0 ? remainingSegments[0] : null;
  }

  private static void AnalyzeSingleFile(
      string filePath,
      string relativePath,
      ICollection<SuppressedSymbolInfo> output,
      CancellationToken cancellationToken)
  {
    var text = File.ReadAllText(filePath);
    var syntaxTree = CSharpSyntaxTree.ParseText(text, cancellationToken: cancellationToken);
    var root = syntaxTree.GetRoot(cancellationToken);

    var walker = new SuppressMessageWalker(relativePath, output);
    walker.Visit(root);
  }

  private sealed class SuppressMessageWalker : CSharpSyntaxWalker
  {
    private readonly string _relativePath;
    private readonly ICollection<SuppressedSymbolInfo> _output;
    private readonly Stack<string> _namespaceStack = new();
    private readonly Stack<string> _typeStack = new();

    public SuppressMessageWalker(string relativePath, ICollection<SuppressedSymbolInfo> output)
      : base(SyntaxWalkerDepth.StructuredTrivia)
    {
      _relativePath = relativePath;
      _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
    {
      if (node is null)
      {
        return;
      }

      _namespaceStack.Push(node.Name.ToString());
      base.VisitNamespaceDeclaration(node);
      _namespaceStack.Pop();
    }

    public override void VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
    {
      if (node is null)
      {
        return;
      }

      _namespaceStack.Push(node.Name.ToString());
      base.VisitFileScopedNamespaceDeclaration(node);
      _namespaceStack.Pop();
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
      if (node is null)
      {
        return;
      }

      _typeStack.Push(node.Identifier.Text);
      TryRecordSuppression(node.AttributeLists, BuildTypeFqn());
      base.VisitClassDeclaration(node);
      _typeStack.Pop();
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
      if (node is null)
      {
        return;
      }

      _typeStack.Push(node.Identifier.Text);
      TryRecordSuppression(node.AttributeLists, BuildTypeFqn());
      base.VisitStructDeclaration(node);
      _typeStack.Pop();
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
      if (node is null)
      {
        return;
      }

      _typeStack.Push(node.Identifier.Text);
      TryRecordSuppression(node.AttributeLists, BuildTypeFqn());
      base.VisitRecordDeclaration(node);
      _typeStack.Pop();
    }

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
      if (node is null)
      {
        return;
      }

      _typeStack.Push(node.Identifier.Text);
      TryRecordSuppression(node.AttributeLists, BuildTypeFqn());
      base.VisitInterfaceDeclaration(node);
      _typeStack.Pop();
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
      if (node is null)
      {
        return;
      }

      var memberFqn = BuildMemberFqn(node.Identifier.Text);
      TryRecordSuppression(node.AttributeLists, memberFqn);
      base.VisitMethodDeclaration(node);
    }

    public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
      if (node is null)
      {
        return;
      }

      // Constructors are filtered out of the metrics report, but we still record
      // suppressions in case future consumers need them.
      var memberFqn = BuildMemberFqn(node.Identifier.Text);
      TryRecordSuppression(node.AttributeLists, memberFqn);
      base.VisitConstructorDeclaration(node);
    }

    private string? BuildTypeFqn()
    {
      if (_typeStack.Count == 0)
      {
        return null;
      }

      var typeName = string.Join(".", _typeStack.Reverse());
      var ns = _namespaceStack.Count == 0 ? null : string.Join(".", _namespaceStack.Reverse());
      return string.IsNullOrWhiteSpace(ns) ? typeName : ns + "." + typeName;
    }

    private static string? NormalizeMemberFqn(string? rawMemberFqn)
      => Processing.SymbolNormalizer.NormalizeFullyQualifiedMethodName(rawMemberFqn);

    private string? BuildMemberFqn(string identifier)
    {
      var typeFqn = BuildTypeFqn();
      if (string.IsNullOrWhiteSpace(typeFqn))
      {
        return null;
      }

      // Parameter details are not required because the normalizer will collapse
      // them to "(...)" and only preserve the namespace/type/method name chain.
      var raw = $"{typeFqn}.{identifier}()";
      return NormalizeMemberFqn(raw);
    }

    private void TryRecordSuppression(SyntaxList<AttributeListSyntax> attributeLists, string? fullyQualifiedName)
    {
      if (string.IsNullOrWhiteSpace(fullyQualifiedName))
      {
        return;
      }

      foreach (var attributeList in attributeLists)
      {
        foreach (var attribute in attributeList.Attributes)
        {
          if (!TryParseSuppressMessage(attribute, out var ruleId, out var justification))
          {
            continue;
          }

          SuppressedRuleMetricMapper.TryGetMetricName(ruleId, out var metricName);
          metricName ??= ruleId;
          if (string.IsNullOrWhiteSpace(metricName))
          {
            continue;
          }

          _output.Add(new SuppressedSymbolInfo
          {
            FilePath = _relativePath,
            FullyQualifiedName = fullyQualifiedName,
            RuleId = ruleId ?? string.Empty,
            Metric = metricName,
            Justification = justification
          });
        }
      }
    }

    private static bool TryParseSuppressMessage(
        AttributeSyntax attribute,
        out string? ruleId,
        out string? justification)
    {
      ruleId = null;
      justification = null;

      if (!IsSuppressMessageAttribute(attribute))
      {
        return false;
      }

      if (attribute.ArgumentList is null || attribute.ArgumentList.Arguments.Count < 2)
      {
        return false;
      }

      // SuppressMessage(string category, string checkId)
      var args = attribute.ArgumentList.Arguments;

      var categoryLiteral = args[0].Expression as LiteralExpressionSyntax;
      var category = categoryLiteral?.Token.ValueText;
      if (string.IsNullOrWhiteSpace(category) ||
          (!category.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) &&
           !category.Equals("Style", StringComparison.OrdinalIgnoreCase)))
      {
        return false;
      }

      var checkIdLiteral = args[1].Expression as LiteralExpressionSyntax;
      var checkIdValue = checkIdLiteral?.Token.ValueText;
      if (string.IsNullOrWhiteSpace(checkIdValue))
      {
        return false;
      }

      var colonIndex = checkIdValue.IndexOf(':', StringComparison.Ordinal);
      ruleId = colonIndex > 0 ? checkIdValue[..colonIndex] : checkIdValue;

      foreach (var argument in args)
      {
        if (argument.NameEquals is null)
        {
          continue;
        }

        if (!string.Equals(argument.NameEquals.Name.Identifier.Text, "Justification", StringComparison.Ordinal))
        {
          continue;
        }

        // WHY: Justification can be a single string literal or a concatenation of multiple
        // string literals (e.g., "string1" + "string2" + "string3"). We need to handle both cases
        // by recursively extracting string literals from binary expressions with the '+' operator.
        justification = ExtractJustificationText(argument.Expression);
        break;
      }

      return !string.IsNullOrWhiteSpace(ruleId);
    }

    /// <summary>
    /// Extracts justification text from an expression, handling both single string literals
    /// and string concatenation expressions (e.g., "string1" + "string2").
    /// </summary>
    /// <param name="expression">The expression to extract text from.</param>
    /// <returns>
    /// The extracted justification text, or <see langword="null"/> if the expression
    /// does not contain string literals.
    /// </returns>
    private static string? ExtractJustificationText(ExpressionSyntax? expression)
    {
      if (expression is null)
      {
        return null;
      }

      // Single string literal case
      if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
      {
        return literal.Token.ValueText;
      }

      // String concatenation case: "string1" + "string2" + ...
      if (expression is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression))
      {
        var parts = new List<string>();

        // WHY: Recursively collect all string literals from the left and right sides
        // of the binary expression. This handles nested concatenations like:
        // "string1" + ("string2" + "string3")
        CollectStringLiterals(binary, parts);

        return parts.Count > 0 ? string.Concat(parts) : null;
      }

      return null;
    }

    /// <summary>
    /// Recursively collects string literals from a binary expression tree.
    /// </summary>
    /// <param name="expression">The expression to traverse.</param>
    /// <param name="parts">The list to collect string literal values into.</param>
    private static void CollectStringLiterals(ExpressionSyntax expression, List<string> parts)
    {
      if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
      {
        var text = literal.Token.ValueText;
        if (!string.IsNullOrEmpty(text))
        {
          parts.Add(text);
        }
        return;
      }

      if (expression is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression))
      {
        // Traverse left and right subtrees
        CollectStringLiterals(binary.Left, parts);
        CollectStringLiterals(binary.Right, parts);
      }
    }

    private static bool IsSuppressMessageAttribute(AttributeSyntax attribute)
    {
      // WHY: Support both short form (SuppressMessage) and fully qualified form
      // (System.Diagnostics.CodeAnalysis.SuppressMessage). The attribute name can be parsed
      // as a simple identifier, qualified name, or alias-qualified name by Roslyn.
      // We need to handle all cases to ensure suppressions work regardless of using directives.
      
      // Check the simple name (last identifier in the qualified name chain)
      string? simpleName = null;
      
      if (attribute.Name is SimpleNameSyntax simpleNameSyntax)
      {
        simpleName = simpleNameSyntax.Identifier.Text;
      }
      else if (attribute.Name is QualifiedNameSyntax qualifiedName)
      {
        // WHY: QualifiedNameSyntax has Left (NameSyntax) and Right (SimpleNameSyntax).
        // For "System.Diagnostics.CodeAnalysis.SuppressMessage", it's parsed as:
        // QualifiedName(QualifiedName(QualifiedName(System, Diagnostics), CodeAnalysis), SuppressMessage)
        // The Right property always contains the rightmost SimpleNameSyntax, which is the actual attribute name.
        // No traversal needed - Right is always the simple name we want.
        simpleName = qualifiedName.Right.Identifier.Text;
      }

      if (string.IsNullOrEmpty(simpleName))
      {
        // Fallback to string comparison if structure parsing fails
        var name = attribute.Name.ToString();
        return name.EndsWith("SuppressMessage", StringComparison.Ordinal) ||
               name.EndsWith("SuppressMessageAttribute", StringComparison.Ordinal);
      }

      // Check if the simple name matches (with or without "Attribute" suffix)
      return simpleName.Equals("SuppressMessage", StringComparison.Ordinal) ||
             simpleName.Equals("SuppressMessageAttribute", StringComparison.Ordinal);
    }
  }
}


