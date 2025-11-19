namespace Rca.Tools.MetricsReporter.Aggregation;

using System;
using System.Collections.Generic;
using System.Linq;
using Rca.Tools.MetricsReporter.Model;
using Rca.Tools.MetricsReporter.Processing;

/// <summary>
/// Builds the consolidated report from parsed metrics sources.
/// </summary>
public sealed class MetricsAggregationService
{
  private readonly MemberFilter _memberFilter;
  private readonly AssemblyFilter _assemblyFilter;
  private readonly TypeFilter _typeFilter;

  /// <summary>
  /// Initializes a new instance of the <see cref="MetricsAggregationService"/> class with default filters.
  /// </summary>
  public MetricsAggregationService()
      : this(new MemberFilter(), new AssemblyFilter(), new TypeFilter())
  {
  }

  /// <summary>
  /// Initializes a new instance of the <see cref="MetricsAggregationService"/> class with the specified filters.
  /// </summary>
  /// <param name="memberFilter">The member filter to use for excluding methods. Cannot be null.</param>
  /// <param name="assemblyFilter">The assembly filter to use for excluding assemblies. Cannot be null.</param>
  /// <param name="typeFilter">The type filter to use for excluding types. Cannot be null.</param>
  /// <exception cref="ArgumentNullException">Thrown when any of the filters are null.</exception>
  public MetricsAggregationService(MemberFilter memberFilter, AssemblyFilter assemblyFilter, TypeFilter typeFilter)
  {
    ArgumentNullException.ThrowIfNull(memberFilter);
    ArgumentNullException.ThrowIfNull(assemblyFilter);
    ArgumentNullException.ThrowIfNull(typeFilter);
    _memberFilter = memberFilter;
    _assemblyFilter = assemblyFilter;
    _typeFilter = typeFilter;
  }

  /// <summary>
  /// Creates the final metrics report.
  /// </summary>
  /// <param name="input">Aggregation input data.</param>
  /// <returns>Composed metrics report.</returns>
  public MetricsReport BuildReport(MetricsAggregationInput input)
  {
    ArgumentNullException.ThrowIfNull(input);

    var workspace = new AggregationWorkspace(input.SolutionName, _memberFilter, _assemblyFilter, _typeFilter);

    foreach (var document in input.AltCoverDocuments)
    {
      workspace.MergeStructuralElements(document);
    }

    foreach (var document in input.RoslynDocuments)
    {
      workspace.MergeStructuralElements(document);
    }

    workspace.BuildLineIndex();

    foreach (var document in input.SarifDocuments)
    {
      workspace.ApplySarifDocument(document);
    }

    // Reconcile iterator state-machine coverage so that methods like X(...)
    // receive coverage from compiler-generated nested types <X>d__N before
    // baseline deltas and thresholds are applied.
    workspace.ReconcileIteratorStateMachineMetrics();

    // Reconcile coverage for plain nested plus-types (e.g., Root+Leaf or Root+Inner+Leaf)
    // by transferring coverage into corresponding dot types (Namespace.Root.Leaf, Namespace.Root.Inner.Leaf)
    // when there is no conflicting coverage on the target types/members.
    workspace.ReconcilePlainNestedTypeMetrics();

    workspace.ApplyBaselineAndThresholds(input.Baseline, input.Thresholds);

    var (thresholdLevels, thresholdDescriptions) = CreateMetadataThresholds(input.Thresholds);

    var metadata = new ReportMetadata
    {
      GeneratedAtUtc = DateTime.UtcNow,
      BaselineReference = input.BaselineReference,
      Paths = input.Paths,
      ThresholdsByLevel = thresholdLevels,
      ThresholdDescriptions = thresholdDescriptions,
      ExcludedMemberNamesPatterns = _memberFilter.GetExcludedMemberNamesPatternsString(),
      ExcludedAssemblyNames = _assemblyFilter.GetExcludedAssemblyPatternsString(),
      ExcludedTypeNamePatterns = _typeFilter.GetExcludedTypePatternsString()
    };

    return new MetricsReport
    {
      Metadata = metadata,
      Solution = workspace.Solution
    };
  }

  /// <summary>
  /// Creates metadata structures from threshold definitions for report serialization.
  /// </summary>
  /// <param name="thresholds">The threshold definitions to process.</param>
  /// <returns>
  /// A tuple containing:
  /// - Thresholds grouped by symbol level
  /// - Metric descriptions
  /// </returns>
  /// <remarks>
  /// This method transforms the detailed threshold definitions into metadata structures:
  /// - Per-level thresholds preserve all symbol-level distinctions
  /// - Descriptions are extracted for tooltip display in HTML reports
  /// </remarks>
  private static (Dictionary<MetricIdentifier, IDictionary<MetricSymbolLevel, MetricThreshold>> ThresholdsByLevel,
      Dictionary<MetricIdentifier, string?> Descriptions) CreateMetadataThresholds(
      IDictionary<MetricIdentifier, MetricThresholdDefinition> thresholds)
  {
    var perLevelResult = new Dictionary<MetricIdentifier, IDictionary<MetricSymbolLevel, MetricThreshold>>();
    var descriptions = new Dictionary<MetricIdentifier, string?>();

    foreach (var (identifier, definition) in thresholds)
    {
      descriptions[identifier] = definition.Description;
      var clonedLevels = CloneThresholdLevels(definition.Levels);
      perLevelResult[identifier] = clonedLevels;
    }

    return (perLevelResult, descriptions);
  }

  /// <summary>
  /// Clones all threshold levels from a definition to create an independent copy.
  /// </summary>
  /// <param name="levels">The threshold levels to clone.</param>
  /// <returns>A new dictionary with cloned threshold values.</returns>
  private static Dictionary<MetricSymbolLevel, MetricThreshold> CloneThresholdLevels(
      IDictionary<MetricSymbolLevel, MetricThreshold> levels)
  {
    var clonedLevels = new Dictionary<MetricSymbolLevel, MetricThreshold>();
    foreach (var (level, threshold) in levels)
    {
      clonedLevels[level] = CloneThreshold(threshold);
    }

    return clonedLevels;
  }

  /// <summary>
  /// Creates a deep copy of a threshold value.
  /// </summary>
  /// <param name="threshold">The threshold to clone.</param>
  /// <returns>A new threshold instance with the same values.</returns>
  private static MetricThreshold CloneThreshold(MetricThreshold threshold)
      => new()
      {
        Warning = threshold.Warning,
        Error = threshold.Error,
        HigherIsBetter = threshold.HigherIsBetter,
        PositiveDeltaNeutral = threshold.PositiveDeltaNeutral
      };

  private sealed class AggregationWorkspace
  {
    private readonly Dictionary<string, AssemblyMetricsNode> _assemblies = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NamespaceEntry> _namespaces = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<NamespaceEntry>> _namespaceIndex = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TypeEntry> _types = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MemberMetricsNode> _members = new(StringComparer.Ordinal);

    private readonly Dictionary<string, List<IndexedNode>> _memberLineIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<IndexedNode>> _typeLineIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AssemblyMetricsNode> _fileAssemblyMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly MemberFilter _memberFilter;
    private readonly AssemblyFilter _assemblyFilter;
    private readonly TypeFilter _typeFilter;

    public AggregationWorkspace(string solutionName, MemberFilter memberFilter, AssemblyFilter assemblyFilter, TypeFilter typeFilter)
    {
      Solution = new SolutionMetricsNode
      {
        Name = solutionName,
        FullyQualifiedName = solutionName,
        Metrics = new Dictionary<MetricIdentifier, MetricValue>()
      };
      _memberFilter = memberFilter;
      _assemblyFilter = assemblyFilter;
      _typeFilter = typeFilter;
    }

    public SolutionMetricsNode Solution { get; }

    public void MergeStructuralElements(ParsedMetricsDocument document)
    {
      if (document.Elements.Count == 0)
      {
        return;
      }

      foreach (var element in document.Elements)
      {
        switch (element.Kind)
        {
          case CodeElementKind.Assembly:
            MergeAssembly(element);
            break;
          case CodeElementKind.Namespace:
            MergeNamespace(element);
            break;
          case CodeElementKind.Type:
            MergeType(element);
            break;
          case CodeElementKind.Member:
            MergeMember(element);
            break;
          default:
            break;
        }
      }
    }

    /// <summary>
    /// Builds line-based indexes for members and types to enable efficient lookup by file path and line number.
    /// </summary>
    /// <remarks>
    /// This method processes all members and types, filtering out excluded assemblies,
    /// and creates sorted indexes for fast retrieval during SARIF document processing.
    /// </remarks>
    public void BuildLineIndex()
    {
      IndexMembers();
      IndexTypes();
      SortLineIndexes();
    }

    /// <summary>
    /// Indexes all members by their source file path and line numbers.
    /// </summary>
    /// <remarks>
    /// Members from excluded assemblies are skipped. The method validates source information
    /// and resolves assembly membership before adding to the index.
    /// </remarks>
    private void IndexMembers()
    {
      foreach (var member in _members.Values)
      {
        if (!HasValidSource(member.Source))
        {
          continue;
        }

        if (ShouldExcludeMember(member))
        {
          continue;
        }

        AddToMemberLineIndex(member);
      }
    }

    /// <summary>
    /// Indexes all types by their source file path and line numbers.
    /// </summary>
    /// <remarks>
    /// Types from excluded assemblies are skipped. The method validates source information
    /// before adding to the index.
    /// </remarks>
    private void IndexTypes()
    {
      foreach (var typeEntry in _types.Values)
      {
        var type = typeEntry.Node;
        if (!HasValidSource(type.Source))
        {
          continue;
        }

        if (ShouldExcludeType(typeEntry))
        {
          continue;
        }

        AddToTypeLineIndex(type, typeEntry);
      }
    }

    /// <summary>
    /// Sorts all line indexes by start line number for efficient binary search.
    /// </summary>
    private void SortLineIndexes()
    {
      foreach (var list in _memberLineIndex.Values)
      {
        list.Sort(static (a, b) => a.StartLine.CompareTo(b.StartLine));
      }

      foreach (var list in _typeLineIndex.Values)
      {
        list.Sort(static (a, b) => a.StartLine.CompareTo(b.StartLine));
      }
    }

    /// <summary>
    /// Checks if a source location has valid path and start line information.
    /// </summary>
    /// <param name="source">The source location to validate.</param>
    /// <returns><see langword="true"/> if the source has a path and start line; otherwise, <see langword="false"/>.</returns>
    private static bool HasValidSource(SourceLocation? source)
        => source?.Path is not null && source.StartLine.HasValue;

    /// <summary>
    /// Determines if a member should be excluded from indexing based on assembly membership.
    /// </summary>
    /// <param name="member">The member to check.</param>
    /// <returns><see langword="true"/> if the member should be excluded; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// A member is excluded if it belongs to an excluded assembly or if its assembly
    /// is not present in the solution. The method attempts to resolve the assembly through
    /// the member's declaring type, or by extracting it from the fully qualified name.
    /// </remarks>
    private bool ShouldExcludeMember(MemberMetricsNode member)
    {
      if (member.FullyQualifiedName is null)
      {
        return false;
      }

      var assemblyName = ResolveMemberAssemblyName(member);
      return assemblyName is not null && ShouldExcludeAssembly(assemblyName);
    }

    /// <summary>
    /// Resolves the assembly name for a member by checking its declaring type or extracting from FQN.
    /// </summary>
    /// <param name="member">The member to resolve.</param>
    /// <returns>The assembly name if found; otherwise, <see langword="null"/>.</returns>
    private string? ResolveMemberAssemblyName(MemberMetricsNode member)
    {
      var memberTypeFqn = ResolveDeclaringType(member.FullyQualifiedName!);
      if (memberTypeFqn is not null && _types.TryGetValue(memberTypeFqn, out var memberTypeEntry))
      {
        return memberTypeEntry.Assembly.FullyQualifiedName;
      }

      // If we can't resolve the assembly through type, try to extract it from FQN
      // FullyQualifiedName is guaranteed to be non-null here due to check in ShouldExcludeMember
      return ResolveAssemblyNameFromFqn(member.FullyQualifiedName!);
    }

    /// <summary>
    /// Determines if an assembly should be excluded from indexing.
    /// </summary>
    /// <param name="assemblyName">The assembly name to check.</param>
    /// <returns><see langword="true"/> if the assembly should be excluded; otherwise, <see langword="false"/>.</returns>
    private bool ShouldExcludeAssembly(string? assemblyName)
    {
      if (assemblyName is null)
      {
        return false;
      }

      return _assemblyFilter.ShouldExcludeAssembly(assemblyName) || !_assemblies.ContainsKey(assemblyName);
    }

    /// <summary>
    /// Determines if a type should be excluded from indexing based on assembly membership.
    /// </summary>
    /// <param name="typeEntry">The type entry to check.</param>
    /// <returns><see langword="true"/> if the type should be excluded; otherwise, <see langword="false"/>.</returns>
    private bool ShouldExcludeType(TypeEntry typeEntry)
    {
      var assemblyName = typeEntry.Assembly.FullyQualifiedName;
      return ShouldExcludeAssembly(assemblyName);
    }

    /// <summary>
    /// Adds a member to the line index for its source file.
    /// </summary>
    /// <param name="member">The member to index.</param>
    /// <remarks>
    /// Creates the index entry if it doesn't exist, then adds the member with its line range.
    /// Also registers the file-to-assembly mapping for SARIF processing.
    /// </remarks>
    private void AddToMemberLineIndex(MemberMetricsNode member)
    {
      var start = member.Source!.StartLine!.Value;
      var end = member.Source.EndLine ?? start;
      var normalizedPath = NormalizePath(member.Source.Path!);

      var list = GetOrCreateIndexList(_memberLineIndex, normalizedPath);
      list.Add(new IndexedNode(member, start, end));
      RegisterFileAssembly(normalizedPath, member);
    }

    /// <summary>
    /// Adds a type to the line index for its source file.
    /// </summary>
    /// <param name="type">The type node to index.</param>
    /// <param name="typeEntry">The type entry containing assembly information.</param>
    /// <remarks>
    /// Creates the index entry if it doesn't exist, then adds the type with its line range.
    /// Also registers the file-to-assembly mapping for SARIF processing.
    /// </remarks>
    private void AddToTypeLineIndex(TypeMetricsNode type, TypeEntry typeEntry)
    {
      var start = type.Source!.StartLine!.Value;
      var end = type.Source.EndLine ?? start;
      var normalizedPath = NormalizePath(type.Source.Path!);

      var list = GetOrCreateIndexList(_typeLineIndex, normalizedPath);
      list.Add(new IndexedNode(type, start, end));
      RegisterFileAssembly(normalizedPath, typeEntry.Assembly);
    }

    /// <summary>
    /// Gets an existing index list or creates a new one for the specified path.
    /// </summary>
    /// <param name="index">The dictionary containing the index.</param>
    /// <param name="path">The normalized file path.</param>
    /// <returns>The list of indexed nodes for the path.</returns>
    private static List<IndexedNode> GetOrCreateIndexList(
        Dictionary<string, List<IndexedNode>> index,
        string path)
    {
      if (!index.TryGetValue(path, out var list))
      {
        list = new List<IndexedNode>();
        index[path] = list;
      }

      return list;
    }

    public void ApplySarifDocument(ParsedMetricsDocument document)
    {
      foreach (var element in document.Elements)
      {
        if (element.Source?.Path is null)
        {
          continue;
        }

        if (element.Metrics.Count == 0)
        {
          continue;
        }

        var metric = element.Metrics.First();
        if (metric.Value.Value is null)
        {
          continue;
        }

        var line = element.Source.StartLine ?? element.Source.EndLine;
        var normalizedPath = NormalizePath(element.Source.Path);

        MetricsNode? target = null;

        if (line.HasValue)
        {
          target = FindNodeInIndex(_memberLineIndex, normalizedPath, line.Value);
          target ??= FindNodeInIndex(_typeLineIndex, normalizedPath, line.Value);
        }

        if (target is null && _fileAssemblyMap.TryGetValue(normalizedPath, out var assembly))
        {
          // Skip SARIF metrics for excluded assemblies
          if (_assemblyFilter.ShouldExcludeAssembly(assembly.FullyQualifiedName))
          {
            continue;
          }
          target = assembly;
        }

        target ??= Solution;

        MergeMetric(target, metric.Key, metric.Value, aggregate: true);
      }
    }

    public void ApplyBaselineAndThresholds(
        MetricsReport? baseline,
        IDictionary<MetricIdentifier, MetricThresholdDefinition> thresholds)
    {
      var baselineLookup = CreateBaselineLookup(baseline?.Solution);
      ApplyBaselineRecursive(Solution, baselineLookup, thresholds, Solution.Name);
    }

    /// <summary>
    /// Reconciles coverage for compiler-generated iterator state machine types
    /// (for example, nested types with names following the pattern <c>SomeType+&lt;X&gt;d__N</c>)
    /// by transferring their AltCover coverage back to the corresponding user-defined
    /// method <c>X(...)</c> on <c>SomeType</c>.
    /// </summary>
    /// <remarks>
    /// The reconciliation is applied conservatively:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// If the target method <c>X(...)</c> cannot be found on the outer type,
    /// the iterator type is left untouched.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// If the target method already has non-zero AltCover sequence or branch coverage,
    /// no transfer is performed to avoid overwriting existing data.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Only when coverage is successfully transferred is the iterator type removed
    /// from the hierarchy so that it does not appear in the HTML report.
    /// </description>
    /// </item>
    /// </list>
    /// The method also marks the affected member with
    /// <see cref="MemberMetricsNode.IncludesIteratorStateMachineCoverage"/> so that
    /// the HTML renderer can annotate it with a neutral indicator glyph.
    /// </remarks>
    public void ReconcileIteratorStateMachineMetrics()
    {
      if (_types.Count == 0)
      {
        return;
      }

      var iteratorTypeKeys = new List<string>();
      foreach (var key in _types.Keys)
      {
        if (TryExtractIteratorInfo(key, out _, out _))
        {
          iteratorTypeKeys.Add(key);
        }
      }

      if (iteratorTypeKeys.Count == 0)
      {
        return;
      }

      foreach (var iteratorTypeKey in iteratorTypeKeys)
      {
        if (!_types.TryGetValue(iteratorTypeKey, out var iteratorTypeEntry))
        {
          continue;
        }

        if (!TryExtractIteratorInfo(iteratorTypeKey, out var outerTypeFqn, out var methodName))
        {
          continue;
        }

        if (!_types.TryGetValue(outerTypeFqn, out var outerTypeEntry))
        {
          // Outer type not found – keep iterator type as-is.
          continue;
        }

        var targetMember = FindMethodOnType(outerTypeEntry.Node, methodName);
        if (targetMember is null)
        {
          // No matching method – keep iterator type as-is.
          continue;
        }

        var methodHasCoverage = HasNonZeroAltCoverCoverage(targetMember.Metrics);
        var iteratorHasCoverage = HasNonZeroAltCoverCoverage(iteratorTypeEntry.Node.Metrics);

        if (methodHasCoverage && iteratorHasCoverage)
        {
          // Both method and iterator have coverage – keep them separate to avoid
          // overriding or double-counting.
          continue;
        }

        if (!methodHasCoverage && !iteratorHasCoverage)
        {
          // Neither method nor iterator carry useful coverage – treat the iterator
          // type as non-informative noise and hide it from the report.
          RemoveIteratorTypeFromHierarchy(iteratorTypeKey, iteratorTypeEntry);
          continue;
        }

        if (!methodHasCoverage && iteratorHasCoverage)
        {
          // Iterator type carries the real coverage – transfer it to the method
          // and hide the compiler-generated type.
          TransferIteratorCoverage(iteratorTypeEntry.Node, targetMember);
          RemoveIteratorTypeFromHierarchy(iteratorTypeKey, iteratorTypeEntry);
        }

        // When method already has coverage and iterator does not, we keep the iterator
        // type as-is because it may still be useful for low-level diagnostics.
      }
    }

    /// <summary>
    /// Reconciles coverage for plain nested plus-types (for example,
    /// <c>Namespace.Root+Leaf</c> or <c>Namespace.Root+Inner+Leaf</c>)
    /// by transferring their AltCover coverage into corresponding dot types
    /// (<c>Namespace.Root.Leaf</c>, <c>Namespace.Root.Inner.Leaf</c>)
    /// when there is no conflicting coverage on the target types or members.
    /// </summary>
    /// <remarks>
    /// The reconciliation is conservative:
    /// <list type="bullet">
    /// <item>
    /// <description>If no matching dot type exists, the nested plus-type is left unchanged.</description>
    /// </item>
    /// <item>
    /// <description>
    /// If both the plus-type and the dot type have non-zero AltCover coverage,
    /// the transfer is cancelled to avoid mixing independent metrics.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// If any pair of corresponding methods on the plus-type and dot type both
    /// have non-zero AltCover coverage, the transfer is cancelled for the whole type.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// When transfer succeeds, methods on the dot type that receive coverage are
    /// marked with <see cref="MemberMetricsNode.IncludesIteratorStateMachineCoverage"/>
    /// so that the HTML renderer can annotate them with a neutral indicator glyph.
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    public void ReconcilePlainNestedTypeMetrics()
    {
      if (_types.Count == 0)
      {
        return;
      }

      var candidateTypeKeys = new List<string>();
      foreach (var key in _types.Keys)
      {
        if (TryParsePlainNestedPlusType(key, out _, out _, out _))
        {
          candidateTypeKeys.Add(key);
        }
      }

      if (candidateTypeKeys.Count == 0)
      {
        return;
      }

      foreach (var plusTypeKey in candidateTypeKeys)
      {
        if (!_types.TryGetValue(plusTypeKey, out var plusTypeEntry))
        {
          continue;
        }

        if (!TryParsePlainNestedPlusType(plusTypeKey, out var namespaceFqn, out var segments, out var dotTypeFqn))
        {
          continue;
        }

        if (!_types.TryGetValue(dotTypeFqn, out var dotTypeEntry))
        {
          // Dot type does not exist – keep the plus-type as-is.
          continue;
        }

        // Type-level coverage conflict check
        var plusTypeHasCoverage = HasNonZeroAltCoverCoverage(plusTypeEntry.Node.Metrics);
        var dotTypeHasCoverage = HasNonZeroAltCoverCoverage(dotTypeEntry.Node.Metrics);
        if (plusTypeHasCoverage && dotTypeHasCoverage)
        {
          // Both types have coverage – avoid mixing.
          continue;
        }

        // Method-level conflict detection
        if (HasMethodCoverageConflict(plusTypeEntry.Node, dotTypeEntry.Node))
        {
          // At least one method has coverage on both sides – cancel transfer.
          continue;
        }

        // Transfer type-level coverage when only plus-type has AltCover coverage.
        if (plusTypeHasCoverage && !dotTypeHasCoverage)
        {
          TransferTypeAltCoverCoverage(plusTypeEntry.Node, dotTypeEntry.Node);
        }

        // Transfer method-level coverage and create missing methods when necessary.
        TransferMethodCoverageFromPlusType(plusTypeEntry.Node, dotTypeEntry.Node, plusTypeKey, dotTypeFqn);

        // Remove the reconciled plus-type from the hierarchy.
        RemoveIteratorTypeFromHierarchy(plusTypeKey, plusTypeEntry);
      }
    }

    private static bool TryExtractIteratorInfo(string typeFqn, out string outerTypeFqn, out string methodName)
    {
      outerTypeFqn = string.Empty;
      methodName = string.Empty;

      if (string.IsNullOrWhiteSpace(typeFqn))
      {
        return false;
      }

      var plusIndex = typeFqn.LastIndexOf('+');
      if (plusIndex <= 0 || plusIndex >= typeFqn.Length - 1)
      {
        return false;
      }

      var nestedPart = typeFqn[(plusIndex + 1)..];
      // Expected pattern: <MethodName>d__N
      if (!nestedPart.StartsWith('<') || nestedPart.IndexOf('>') is var closeIndex && closeIndex <= 1)
      {
        return false;
      }

      var endOfName = nestedPart.IndexOf('>');
      if (endOfName <= 1 || endOfName >= nestedPart.Length - 1)
      {
        return false;
      }

      var suffix = nestedPart[(endOfName + 1)..];
      if (!suffix.StartsWith("d__"))
      {
        return false;
      }

      // Ensure suffix after d__ is numeric to avoid false positives.
      var numberPart = suffix["d__".Length..];
      if (numberPart.Length == 0 || !int.TryParse(numberPart, out _))
      {
        return false;
      }

      outerTypeFqn = typeFqn[..plusIndex];
      methodName = nestedPart[1..endOfName];
      return !string.IsNullOrWhiteSpace(outerTypeFqn) && !string.IsNullOrWhiteSpace(methodName);
    }

    private static MemberMetricsNode? FindMethodOnType(TypeMetricsNode typeNode, string methodName)
    {
      foreach (var member in typeNode.Members)
      {
        if (string.IsNullOrWhiteSpace(member.FullyQualifiedName))
        {
          continue;
        }

        var extractedName = SymbolNormalizer.ExtractMethodName(member.FullyQualifiedName);
        if (string.Equals(extractedName, methodName, StringComparison.Ordinal))
        {
          return member;
        }
      }

      return null;
    }

    private static bool HasNonZeroAltCoverCoverage(IDictionary<MetricIdentifier, MetricValue> metrics)
    {
      if (metrics.TryGetValue(MetricIdentifier.AltCoverSequenceCoverage, out var seq) &&
          seq.Value.HasValue && seq.Value.Value != 0)
      {
        return true;
      }

      if (metrics.TryGetValue(MetricIdentifier.AltCoverBranchCoverage, out var br) &&
          br.Value.HasValue && br.Value.Value != 0)
      {
        return true;
      }

      return false;
    }

    private static void TransferIteratorCoverage(TypeMetricsNode iteratorType, MemberMetricsNode targetMember)
    {
      // Move primary AltCover coverage metrics from the iterator type to the method,
      // but only when the method currently has no meaningful coverage.
      CopyAltCoverMetricIfPresent(iteratorType.Metrics, targetMember.Metrics, MetricIdentifier.AltCoverSequenceCoverage);
      CopyAltCoverMetricIfPresent(iteratorType.Metrics, targetMember.Metrics, MetricIdentifier.AltCoverBranchCoverage);
      CopyAltCoverMetricIfPresent(iteratorType.Metrics, targetMember.Metrics, MetricIdentifier.AltCoverCyclomaticComplexity);
      CopyAltCoverMetricIfPresent(iteratorType.Metrics, targetMember.Metrics, MetricIdentifier.AltCoverNPathComplexity);

      targetMember.IncludesIteratorStateMachineCoverage = true;
    }

    private static bool TryParsePlainNestedPlusType(
        string typeFqn,
        out string namespaceFqn,
        out string[] segments,
        out string dotTypeFqn)
    {
      namespaceFqn = ResolveNamespaceName(typeFqn);
      dotTypeFqn = string.Empty;
      segments = Array.Empty<string>();

      if (string.IsNullOrWhiteSpace(typeFqn))
      {
        return false;
      }

      // Extract the part after the namespace: Root+Leaf or Root+Inner+Leaf
      var nsPrefix = string.IsNullOrWhiteSpace(namespaceFqn) || namespaceFqn == "<global>"
          ? string.Empty
          : namespaceFqn + ".";

      if (!typeFqn.StartsWith(nsPrefix, StringComparison.Ordinal) || typeFqn.Length <= nsPrefix.Length)
      {
        return false;
      }

      var typePart = typeFqn[nsPrefix.Length..];
      if (!typePart.Contains('+', StringComparison.Ordinal))
      {
        return false;
      }

      segments = typePart.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
      if (segments.Length < 2)
      {
        return false;
      }

      // Reject compiler-generated patterns: any segment containing '<', '>' or "__"
      foreach (var segment in segments)
      {
        if (segment.Contains('<', StringComparison.Ordinal) ||
            segment.Contains('>', StringComparison.Ordinal) ||
            segment.Contains("__", StringComparison.Ordinal))
        {
          return false;
        }
      }

      var leafName = segments[^1];
      var parentSegments = segments[..^1];
      var dotNamespace = namespaceFqn == "<global>"
          ? string.Join('.', parentSegments)
          : string.IsNullOrWhiteSpace(namespaceFqn)
              ? string.Join('.', parentSegments)
              : namespaceFqn + "." + string.Join('.', parentSegments);

      dotTypeFqn = string.IsNullOrWhiteSpace(dotNamespace)
          ? leafName
          : dotNamespace + "." + leafName;

      return true;
    }

    private static bool HasMethodCoverageConflict(TypeMetricsNode plusType, TypeMetricsNode dotType)
    {
      if (plusType.Members.Count == 0 || dotType.Members.Count == 0)
      {
        return false;
      }

      var dotMethods = BuildMethodMapByName(dotType);

      foreach (var plusMember in plusType.Members)
      {
        var name = ExtractMethodKey(plusMember);
        if (string.IsNullOrWhiteSpace(name))
        {
          continue;
        }

        if (!dotMethods.TryGetValue(name, out var dotMember))
        {
          continue;
        }

        var plusHasCoverage = HasNonZeroAltCoverCoverage(plusMember.Metrics);
        var dotHasCoverage = HasNonZeroAltCoverCoverage(dotMember.Metrics);

        if (plusHasCoverage && dotHasCoverage)
        {
          return true;
        }
      }

      return false;
    }

    private static Dictionary<string, MemberMetricsNode> BuildMethodMapByName(TypeMetricsNode typeNode)
    {
      var result = new Dictionary<string, MemberMetricsNode>(StringComparer.Ordinal);
      foreach (var member in typeNode.Members)
      {
        var name = ExtractMethodKey(member);
        if (string.IsNullOrWhiteSpace(name))
        {
          continue;
        }

        if (!result.ContainsKey(name))
        {
          result[name] = member;
        }
      }

      return result;
    }

    private static string? ExtractMethodKey(MemberMetricsNode member)
    {
      if (!string.IsNullOrWhiteSpace(member.FullyQualifiedName))
      {
        return SymbolNormalizer.ExtractMethodName(member.FullyQualifiedName);
      }

      return SymbolNormalizer.ExtractMethodName(member.Name);
    }

    private static void TransferTypeAltCoverCoverage(TypeMetricsNode sourceType, TypeMetricsNode targetType)
    {
      CopyAltCoverMetricIfPresent(sourceType.Metrics, targetType.Metrics, MetricIdentifier.AltCoverSequenceCoverage);
      CopyAltCoverMetricIfPresent(sourceType.Metrics, targetType.Metrics, MetricIdentifier.AltCoverBranchCoverage);
      CopyAltCoverMetricIfPresent(sourceType.Metrics, targetType.Metrics, MetricIdentifier.AltCoverCyclomaticComplexity);
      CopyAltCoverMetricIfPresent(sourceType.Metrics, targetType.Metrics, MetricIdentifier.AltCoverNPathComplexity);
    }

    private void TransferMethodCoverageFromPlusType(
        TypeMetricsNode plusType,
        TypeMetricsNode dotType,
        string plusTypeFqn,
        string dotTypeFqn)
    {
      var dotMethodsByName = BuildMethodMapByName(dotType);

      foreach (var plusMember in plusType.Members)
      {
        var methodName = ExtractMethodKey(plusMember);
        if (string.IsNullOrWhiteSpace(methodName))
        {
          continue;
        }

        var plusHasCoverage = HasNonZeroAltCoverCoverage(plusMember.Metrics);
        if (!plusHasCoverage)
        {
          continue;
        }

        dotMethodsByName.TryGetValue(methodName, out var dotMember);
        var dotHasCoverage = dotMember is not null && HasNonZeroAltCoverCoverage(dotMember.Metrics);

        if (dotHasCoverage)
        {
          // We already filtered out conflicts; this case should be rare but safe to skip.
          continue;
        }

        if (dotMember is null)
        {
          var memberFqn = BuildDotMemberFqn(plusMember.FullyQualifiedName, plusTypeFqn, dotTypeFqn, methodName);

          dotMember = new MemberMetricsNode
          {
            Name = ExtractMemberDisplayName(memberFqn, methodName),
            FullyQualifiedName = memberFqn,
            Metrics = new Dictionary<MetricIdentifier, MetricValue>()
          };
          dotType.Members.Add(dotMember);
          if (!string.IsNullOrWhiteSpace(memberFqn))
          {
            _members[memberFqn] = dotMember;
          }
        }

        // Copy AltCover metrics from plus-member to dot-member.
        CopyAltCoverMetricIfPresent(plusMember.Metrics, dotMember.Metrics, MetricIdentifier.AltCoverSequenceCoverage);
        CopyAltCoverMetricIfPresent(plusMember.Metrics, dotMember.Metrics, MetricIdentifier.AltCoverBranchCoverage);
        CopyAltCoverMetricIfPresent(plusMember.Metrics, dotMember.Metrics, MetricIdentifier.AltCoverCyclomaticComplexity);
        CopyAltCoverMetricIfPresent(plusMember.Metrics, dotMember.Metrics, MetricIdentifier.AltCoverNPathComplexity);

        dotMember.IncludesIteratorStateMachineCoverage = true;
      }
    }

    private static string BuildDotMemberFqn(
        string? plusMemberFqn,
        string plusTypeFqn,
        string dotTypeFqn,
        string methodName)
    {
      if (!string.IsNullOrWhiteSpace(plusMemberFqn) &&
          plusMemberFqn.StartsWith(plusTypeFqn, StringComparison.Ordinal) &&
          plusMemberFqn.Length > plusTypeFqn.Length)
      {
        // Replace the type prefix while preserving the normalized method signature suffix "(...)".
        return dotTypeFqn + plusMemberFqn[plusTypeFqn.Length..];
      }

      // Fallback: build a minimal normalized FQN with ellipsis-style parameters.
      return dotTypeFqn + "." + methodName + "(...)";
    }

    private static void CopyAltCoverMetricIfPresent(
        IDictionary<MetricIdentifier, MetricValue> sourceMetrics,
        IDictionary<MetricIdentifier, MetricValue> targetMetrics,
        MetricIdentifier identifier)
    {
      if (!sourceMetrics.TryGetValue(identifier, out var sourceValue) ||
          !sourceValue.Value.HasValue)
      {
        return;
      }

      if (targetMetrics.TryGetValue(identifier, out var existing) &&
          existing.Value.HasValue &&
          existing.Value.Value != 0)
      {
        // Target already has a non-zero value; keep it.
        return;
      }

      targetMetrics[identifier] = new MetricValue
      {
        Value = sourceValue.Value,
        Unit = sourceValue.Unit,
        Status = sourceValue.Status,
        Delta = sourceValue.Delta
      };
    }

    private void RemoveIteratorTypeFromHierarchy(string iteratorTypeKey, TypeEntry iteratorTypeEntry)
    {
      // Remove from type lookup
      _types.Remove(iteratorTypeKey);

      var iteratorTypeNode = iteratorTypeEntry.Node;
      var assembly = iteratorTypeEntry.Assembly;

      // Remove from namespace.Types collection
      foreach (var ns in assembly.Namespaces)
      {
        if (ns.Types.Contains(iteratorTypeNode))
        {
          ns.Types.Remove(iteratorTypeNode);
          break;
        }
      }

      // No need to modify _typeLineIndex or _memberLineIndex here because
      // they are only used during SARIF application which runs before
      // iterator reconciliation.
    }

    private void MergeAssembly(ParsedCodeElement element)
    {
      var assemblyName = element.FullyQualifiedName ?? element.Name;

      // Filter out excluded assemblies
      if (_assemblyFilter.ShouldExcludeAssembly(assemblyName))
      {
        return;
      }

      if (!_assemblies.TryGetValue(assemblyName, out var assemblyNode))
      {
        assemblyNode = new AssemblyMetricsNode
        {
          Name = element.Name,
          FullyQualifiedName = assemblyName,
          Metrics = new Dictionary<MetricIdentifier, MetricValue>()
        };

        _assemblies[assemblyName] = assemblyNode;
        Solution.Assemblies.Add(assemblyNode);
      }

      MergeMetrics(assemblyNode.Metrics, element.Metrics);
      MergeSource(assemblyNode, element.Source);
    }

    private void MergeNamespace(ParsedCodeElement element)
    {
      var assemblyName = element.ParentFullyQualifiedName ?? string.Empty;

      // Filter out namespaces for excluded assemblies
      if (_assemblyFilter.ShouldExcludeAssembly(assemblyName))
      {
        return;
      }

      var namespaceName = element.FullyQualifiedName ?? element.Name;

      var namespaceNode = GetOrCreateNamespace(assemblyName, namespaceName, element.Name);

      // Double-check that namespace's assembly was actually added (not excluded)
      if (!_assemblies.ContainsKey(assemblyName))
      {
        return;
      }

      MergeMetrics(namespaceNode.Node.Metrics, element.Metrics);
      MergeSource(namespaceNode.Node, element.Source);
    }

    private void MergeType(ParsedCodeElement element)
    {
      var typeFqn = element.FullyQualifiedName ?? element.Name;
      var assemblyName = ResolveAssemblyForType(element);

      // Filter out types based on configured type name patterns
      if (_typeFilter.ShouldExcludeType(typeFqn) || _typeFilter.ShouldExcludeType(element.Name))
      {
        return;
      }

      // Filter out types for excluded assemblies
      if (_assemblyFilter.ShouldExcludeAssembly(assemblyName))
      {
        return;
      }

      var namespaceName = ResolveNamespaceName(typeFqn);

      var displayName = string.IsNullOrWhiteSpace(element.Name)
          ? ExtractTypeDisplayName(typeFqn)
          : element.Name.Contains('.') ? ExtractTypeDisplayName(typeFqn) : element.Name;

      var typeEntry = GetOrCreateType(assemblyName, namespaceName, typeFqn, displayName);

      // Double-check that type's assembly was actually added (not excluded)
      if (!_assemblies.ContainsKey(assemblyName))
      {
        return;
      }

      MergeMetrics(typeEntry.Node.Metrics, element.Metrics);
      MergeSource(typeEntry.Node, element.Source);
    }

    private void MergeMember(ParsedCodeElement element)
    {
      if (element.FullyQualifiedName is null)
      {
        // SARIF metrics are aggregated separately by the SARIF pipeline.
        return;
      }

      var memberFqn = element.FullyQualifiedName;

      // Filter out compiler-generated and constructor methods
      if (_memberFilter.ShouldExcludeMethodByFqn(memberFqn))
      {
        return;
      }

      var typeFqn = element.ParentFullyQualifiedName ?? ResolveDeclaringType(memberFqn);

      if (typeFqn is null)
      {
        return;
      }

      // Filter out members belonging to excluded types
      if (_typeFilter.ShouldExcludeType(typeFqn))
      {
        return;
      }

      // Filter out members from excluded assemblies
      var assemblyName = ResolveAssemblyNameFromFqn(typeFqn);
      if (_assemblyFilter.ShouldExcludeAssembly(assemblyName))
      {
        return;
      }

      var typeEntry = EnsureTypeForMember(typeFqn);

      // Double-check that type's assembly was actually added (not excluded)
      if (!_assemblies.ContainsKey(assemblyName))
      {
        return;
      }

      var memberNode = GetOrCreateMember(typeEntry, memberFqn, element.Name);

      MergeMetrics(memberNode.Metrics, element.Metrics);
      MergeSource(memberNode, element.Source);
    }

    private NamespaceEntry GetOrCreateNamespace(string assemblyName, string namespaceFqn, string displayName)
    {
      // Filter out namespaces for excluded assemblies
      if (string.IsNullOrEmpty(assemblyName))
      {
        assemblyName = ResolveAssemblyNameFromFqn(namespaceFqn);
      }

      if (_assemblyFilter.ShouldExcludeAssembly(assemblyName))
      {
        // Return a dummy namespace entry for excluded assemblies
        var dummyAssembly = new AssemblyMetricsNode
        {
          Name = assemblyName,
          FullyQualifiedName = assemblyName,
          Metrics = new Dictionary<MetricIdentifier, MetricValue>()
        };
        var dummyNamespace = new NamespaceMetricsNode
        {
          Name = displayName,
          FullyQualifiedName = namespaceFqn,
          Metrics = new Dictionary<MetricIdentifier, MetricValue>()
        };
        return new NamespaceEntry(dummyNamespace, dummyAssembly);
      }

      var key = $"{assemblyName}::{namespaceFqn}";
      if (_namespaces.TryGetValue(key, out var existingEntry))
      {
        // Verify the existing namespace is not from an excluded assembly
        var existingAssemblyName = existingEntry.Assembly.FullyQualifiedName;
        if (existingAssemblyName is not null && (_assemblyFilter.ShouldExcludeAssembly(existingAssemblyName) || !_assemblies.ContainsKey(existingAssemblyName)))
        {
          // Remove the namespace from dictionaries and from assembly if it's from an excluded assembly
          _namespaces.Remove(key);
          if (_namespaceIndex.TryGetValue(namespaceFqn, out var indexList))
          {
            indexList.Remove(existingEntry);
          }
          // Remove namespace from assembly if it was added
          if (_assemblies.TryGetValue(existingAssemblyName, out var existingAssembly) && existingAssembly.Namespaces.Contains(existingEntry.Node))
          {
            existingAssembly.Namespaces.Remove(existingEntry.Node);
          }
          // Return dummy entry
          var dummyAssembly2 = new AssemblyMetricsNode
          {
            Name = assemblyName,
            FullyQualifiedName = assemblyName,
            Metrics = new Dictionary<MetricIdentifier, MetricValue>()
          };
          var dummyNamespace2 = new NamespaceMetricsNode
          {
            Name = displayName,
            FullyQualifiedName = namespaceFqn,
            Metrics = new Dictionary<MetricIdentifier, MetricValue>()
          };
          return new NamespaceEntry(dummyNamespace2, dummyAssembly2);
        }
        return existingEntry;
      }

      var assembly = GetOrCreateAssembly(assemblyName, new ParsedCodeElement(CodeElementKind.Assembly, assemblyName, assemblyName));

      // Double-check that assembly was actually added (not excluded)
      if (!_assemblies.ContainsKey(assemblyName))
      {
        // Assembly was excluded, return dummy entry
        var dummyNamespace3 = new NamespaceMetricsNode
        {
          Name = displayName,
          FullyQualifiedName = namespaceFqn,
          Metrics = new Dictionary<MetricIdentifier, MetricValue>()
        };
        return new NamespaceEntry(dummyNamespace3, assembly);
      }

      var node = new NamespaceMetricsNode
      {
        Name = displayName,
        FullyQualifiedName = namespaceFqn,
        Metrics = new Dictionary<MetricIdentifier, MetricValue>()
      };

      assembly.Namespaces.Add(node);
      var entry = new NamespaceEntry(node, assembly);
      _namespaces[key] = entry;

      if (!_namespaceIndex.TryGetValue(namespaceFqn, out var list))
      {
        list = new List<NamespaceEntry>();
        _namespaceIndex[namespaceFqn] = list;
      }

      if (!list.Contains(entry))
      {
        list.Add(entry);
      }

      return entry;
    }

    private AssemblyMetricsNode GetOrCreateAssembly(string assemblyName, ParsedCodeElement element)
    {
      // Filter out excluded assemblies
      if (_assemblyFilter.ShouldExcludeAssembly(assemblyName))
      {
        // Return a dummy assembly node that won't be added to Solution
        // This prevents errors when code tries to use the returned assembly
        return new AssemblyMetricsNode
        {
          Name = element.Name,
          FullyQualifiedName = assemblyName,
          Metrics = new Dictionary<MetricIdentifier, MetricValue>()
        };
      }

      if (!_assemblies.TryGetValue(assemblyName, out var assemblyNode))
      {
        assemblyNode = new AssemblyMetricsNode
        {
          Name = element.Name,
          FullyQualifiedName = assemblyName,
          Metrics = new Dictionary<MetricIdentifier, MetricValue>()
        };

        _assemblies[assemblyName] = assemblyNode;
        Solution.Assemblies.Add(assemblyNode);
      }

      MergeMetrics(assemblyNode.Metrics, element.Metrics);
      MergeSource(assemblyNode, element.Source);
      return assemblyNode;
    }

    private TypeEntry GetOrCreateType(string assemblyName, string namespaceName, string typeFqn, string displayName)
    {
      // Check if type already exists, but verify it's not from an excluded assembly
      if (_types.TryGetValue(typeFqn, out var existingEntry))
      {
        // Verify the existing type is not from an excluded assembly
        if (assemblyName is not null && (_assemblyFilter.ShouldExcludeAssembly(assemblyName) || !_assemblies.ContainsKey(assemblyName)))
        {
          // Remove the type from _types if it's from an excluded assembly
          _types.Remove(typeFqn);
          // Remove type from namespace if it was added
          // Find the namespace through the assembly
          if (_assemblies.TryGetValue(existingEntry.Assembly.FullyQualifiedName ?? string.Empty, out var existingAssembly))
          {
            foreach (var ns in existingAssembly.Namespaces)
            {
              if (ns.Types.Contains(existingEntry.Node))
              {
                ns.Types.Remove(existingEntry.Node);
                break;
              }
            }
          }
          // Return a dummy type entry for excluded assemblies
          var dummyAssembly = new AssemblyMetricsNode
          {
            Name = assemblyName,
            FullyQualifiedName = assemblyName,
            Metrics = new Dictionary<MetricIdentifier, MetricValue>()
          };
          var dummyNamespace = new NamespaceMetricsNode
          {
            Name = namespaceName,
            FullyQualifiedName = namespaceName,
            Metrics = new Dictionary<MetricIdentifier, MetricValue>()
          };
          var dummyType = new TypeMetricsNode
          {
            Name = displayName,
            FullyQualifiedName = typeFqn,
            Metrics = new Dictionary<MetricIdentifier, MetricValue>()
          };
          return new TypeEntry(dummyType, dummyAssembly);
        }
        return existingEntry;
      }

      // Filter out types for excluded assemblies
      if (_assemblyFilter.ShouldExcludeAssembly(assemblyName))
      {
        // Return a dummy type entry for excluded assemblies
        var dummyAssembly2 = new AssemblyMetricsNode
        {
          Name = assemblyName,
          FullyQualifiedName = assemblyName,
          Metrics = new Dictionary<MetricIdentifier, MetricValue>()
        };
        var dummyNamespace2 = new NamespaceMetricsNode
        {
          Name = namespaceName,
          FullyQualifiedName = namespaceName,
          Metrics = new Dictionary<MetricIdentifier, MetricValue>()
        };
        var dummyType2 = new TypeMetricsNode
        {
          Name = displayName,
          FullyQualifiedName = typeFqn,
          Metrics = new Dictionary<MetricIdentifier, MetricValue>()
        };
        return new TypeEntry(dummyType2, dummyAssembly2);
      }

      var namespaceEntry = GetOrCreateNamespace(assemblyName, namespaceName, namespaceName);

      // Double-check that namespace's assembly was actually added (not excluded)
      if (!_assemblies.ContainsKey(assemblyName))
      {
        // Assembly was excluded, return dummy entry
        var dummyType3 = new TypeMetricsNode
        {
          Name = displayName,
          FullyQualifiedName = typeFqn,
          Metrics = new Dictionary<MetricIdentifier, MetricValue>()
        };
        return new TypeEntry(dummyType3, namespaceEntry.Assembly);
      }

      var node = new TypeMetricsNode
      {
        Name = displayName,
        FullyQualifiedName = typeFqn,
        Metrics = new Dictionary<MetricIdentifier, MetricValue>()
      };

      namespaceEntry.Node.Types.Add(node);
      var entry = new TypeEntry(node, namespaceEntry.Assembly);
      _types[typeFqn] = entry;

      return entry;
    }

    private TypeEntry EnsureTypeForMember(string typeFqn)
    {
      if (_types.TryGetValue(typeFqn, out var entry))
      {
        return entry;
      }

      var assemblyName = ResolveAssemblyNameFromFqn(typeFqn);
      var namespaceName = ResolveNamespaceName(typeFqn);
      return GetOrCreateType(assemblyName, namespaceName, typeFqn, ExtractTypeDisplayName(typeFqn));
    }

    private MemberMetricsNode GetOrCreateMember(TypeEntry typeEntry, string memberFqn, string displayName)
    {
      // Check if member already exists, but verify it's not from an excluded assembly
      if (_members.TryGetValue(memberFqn, out var existingNode))
      {
        // Verify the existing member is not from an excluded assembly
        var assemblyName = typeEntry.Assembly.FullyQualifiedName;
        if (assemblyName is not null && (_assemblyFilter.ShouldExcludeAssembly(assemblyName) || !_assemblies.ContainsKey(assemblyName)))
        {
          // Remove the member from _members if it's from an excluded assembly
          _members.Remove(memberFqn);
          // Return a dummy member node that won't be added to _members
          return new MemberMetricsNode
          {
            Name = ExtractMemberDisplayName(memberFqn, displayName),
            FullyQualifiedName = memberFqn,
            Metrics = new Dictionary<MetricIdentifier, MetricValue>()
          };
        }
        return existingNode;
      }

      // Don't create members for excluded assemblies
      var assemblyName2 = typeEntry.Assembly.FullyQualifiedName;
      if (assemblyName2 is not null && (_assemblyFilter.ShouldExcludeAssembly(assemblyName2) || !_assemblies.ContainsKey(assemblyName2)))
      {
        // Return a dummy member node that won't be added to _members
        return new MemberMetricsNode
        {
          Name = ExtractMemberDisplayName(memberFqn, displayName),
          FullyQualifiedName = memberFqn,
          Metrics = new Dictionary<MetricIdentifier, MetricValue>()
        };
      }

      var node = new MemberMetricsNode
      {
        Name = ExtractMemberDisplayName(memberFqn, displayName),
        FullyQualifiedName = memberFqn,
        Metrics = new Dictionary<MetricIdentifier, MetricValue>()
      };

      typeEntry.Node.Members.Add(node);
      _members[memberFqn] = node;

      return node;
    }

    private AssemblyMetricsNode? TryResolveAssembly(string namespaceFqn)
    {
      if (!_namespaceIndex.TryGetValue(namespaceFqn, out var entries) || entries.Count == 0)
      {
        return null;
      }

      return entries[0].Assembly;
    }

    private void MergeMetrics(IDictionary<MetricIdentifier, MetricValue> target, IDictionary<MetricIdentifier, MetricValue> source)
    {
      foreach (var pair in source)
      {
        if (target.TryGetValue(pair.Key, out var existing))
        {
          if (IsAggregatableMetric(pair.Key) && pair.Value.Value.HasValue)
          {
            var sum = (existing.Value ?? 0m) + pair.Value.Value.Value;
            target[pair.Key] = new MetricValue
            {
              Value = sum,
              Unit = existing.Unit ?? pair.Value.Unit,
              Status = ThresholdStatus.NotApplicable
            };
          }
          else if (!existing.Value.HasValue && pair.Value.Value.HasValue)
          {
            target[pair.Key] = pair.Value;
          }
        }
        else
        {
          target[pair.Key] = pair.Value;
        }
      }
    }

    private void MergeMetric(MetricsNode node, MetricIdentifier identifier, MetricValue value, bool aggregate)
    {
      if (!node.Metrics.TryGetValue(identifier, out var existing))
      {
        node.Metrics[identifier] = value;
        return;
      }

      if (aggregate && value.Value.HasValue)
      {
        var sum = (existing.Value ?? 0m) + value.Value.Value;
        node.Metrics[identifier] = new MetricValue
        {
          Value = sum,
          Unit = existing.Unit ?? value.Unit,
          Status = ThresholdStatus.NotApplicable
        };
      }
    }

    private static bool IsAggregatableMetric(MetricIdentifier identifier)
        => identifier is MetricIdentifier.SarifCaRuleViolations or MetricIdentifier.SarifIdeRuleViolations;

    private void MergeSource(MetricsNode node, SourceLocation? source)
    {
      if (source is null)
      {
        return;
      }

      if (node.Source is null)
      {
        node.Source = source;
        return;
      }

      if (!node.Source.StartLine.HasValue && source.StartLine.HasValue)
      {
        node.Source = source;
        return;
      }

      if (node.Source.StartLine.HasValue && source.StartLine.HasValue &&
          source.EndLine.HasValue && !node.Source.EndLine.HasValue)
      {
        node.Source = source;
      }
    }

    private string ResolveAssemblyForType(ParsedCodeElement element)
    {
      if (element.ParentFullyQualifiedName is not null && _assemblies.ContainsKey(element.ParentFullyQualifiedName))
      {
        return element.ParentFullyQualifiedName;
      }

      if (element.ParentFullyQualifiedName is not null)
      {
        var assembly = TryResolveAssembly(element.ParentFullyQualifiedName);
        if (assembly is not null)
        {
          return assembly.FullyQualifiedName ?? assembly.Name;
        }
      }

      return ResolveAssemblyNameFromFqn(element.FullyQualifiedName ?? element.Name);
    }

    private string ResolveAssemblyNameFromFqn(string typeFqn)
    {
      var namespaceName = ResolveNamespaceName(typeFqn);
      var assembly = TryResolveAssembly(namespaceName);
      if (assembly is not null)
      {
        return assembly.FullyQualifiedName ?? assembly.Name;
      }

      if (!string.IsNullOrWhiteSpace(typeFqn) && _assemblyFilter.ShouldExcludeAssembly(typeFqn))
      {
        return typeFqn;
      }

      if (!string.IsNullOrWhiteSpace(namespaceName) && !string.Equals(namespaceName, "<global>", StringComparison.Ordinal)
          && _assemblyFilter.ShouldExcludeAssembly(namespaceName))
      {
        return namespaceName;
      }

      var rootNamespace = ExtractRootNamespace(namespaceName);
      if (!string.IsNullOrWhiteSpace(rootNamespace) && _assemblyFilter.ShouldExcludeAssembly(rootNamespace))
      {
        return rootNamespace;
      }

      return _assemblies.Keys.FirstOrDefault() ?? Solution.Name;
    }

    private static string ExtractRootNamespace(string namespaceName)
    {
      if (string.IsNullOrWhiteSpace(namespaceName) || string.Equals(namespaceName, "<global>", StringComparison.Ordinal))
      {
        return string.Empty;
      }

      var separatorIndex = namespaceName.IndexOf('.');
      return separatorIndex < 0 ? namespaceName : namespaceName[..separatorIndex];
    }

    private static string ResolveNamespaceName(string typeFqn)
    {
      var lastDot = typeFqn.LastIndexOf('.');
      return lastDot <= 0 ? "<global>" : typeFqn[..lastDot];
    }

    private static string ExtractTypeDisplayName(string typeFqn)
    {
      var lastDot = typeFqn.LastIndexOf('.');
      return lastDot < 0 ? typeFqn : typeFqn[(lastDot + 1)..];
    }

    /// <summary>
    /// Extracts the display name for a member from its fully qualified name.
    /// </summary>
    /// <param name="memberFqn">The fully qualified member name (e.g., "Namespace.Type.Method(...)").</param>
    /// <param name="fallback">The fallback display name if extraction fails.</param>
    /// <returns>The member display name (e.g., "Method").</returns>
    /// <remarks>
    /// This method handles normalized member names where parameters are replaced with "...".
    /// It extracts the method name part, removing both the type prefix and the parameter placeholder.
    /// </remarks>
    private static string ExtractMemberDisplayName(string memberFqn, string fallback)
    {
      if (string.IsNullOrWhiteSpace(memberFqn))
      {
        return fallback;
      }

      // Find the last dot before the method name
      // Handle normalized signatures like "Namespace.Type.Method(...)"
      var paramStart = memberFqn.IndexOf('(');
      var searchEnd = paramStart >= 0 ? paramStart : memberFqn.Length;

      var lastDot = memberFqn.LastIndexOf('.', searchEnd - 1);
      if (lastDot < 0)
      {
        // No dot found, check if it's just a method name
        if (paramStart >= 0)
        {
          return memberFqn[..paramStart];
        }
        return fallback;
      }

      // Extract method name (between last dot and parameter list)
      var methodNameStart = lastDot + 1;
      var methodNameEnd = paramStart >= 0 ? paramStart : memberFqn.Length;
      var methodName = memberFqn[methodNameStart..methodNameEnd].Trim();

      return string.IsNullOrWhiteSpace(methodName) ? fallback : methodName;
    }

    /// <summary>
    /// Resolves the declaring type name from a member's fully qualified name.
    /// </summary>
    /// <param name="memberFqn">The fully qualified member name (e.g., "Namespace.Type.Method(...)").</param>
    /// <returns>The declaring type's fully qualified name (e.g., "Namespace.Type").</returns>
    /// <remarks>
    /// This method handles normalized member names where parameters are replaced with "...".
    /// It extracts everything before the last dot before the method name.
    /// </remarks>
    private static string ResolveDeclaringType(string memberFqn)
    {
      if (string.IsNullOrWhiteSpace(memberFqn))
      {
        return memberFqn;
      }

      // Find the last dot before the method name (before parameter list)
      var paramStart = memberFqn.IndexOf('(');
      var searchEnd = paramStart >= 0 ? paramStart : memberFqn.Length;

      var lastDot = memberFqn.LastIndexOf('.', searchEnd - 1);
      return lastDot < 0 ? memberFqn : memberFqn[..lastDot];
    }

    private static string NormalizePath(string path)
        => path.Replace('/', '\\').Trim().ToUpperInvariant();

    private void RegisterFileAssembly(string normalizedPath, MetricsNode node)
    {
      // Find the assembly for this node
      AssemblyMetricsNode? assembly = null;
      switch (node)
      {
        case AssemblyMetricsNode a:
          assembly = a;
          break;
        case MemberMetricsNode member when member.FullyQualifiedName is not null:
          var memberTypeFqn = ResolveDeclaringType(member.FullyQualifiedName);
          if (memberTypeFqn is not null && _types.TryGetValue(memberTypeFqn, out var memberTypeEntry))
          {
            assembly = memberTypeEntry.Assembly;
          }
          break;
        case TypeMetricsNode type when type.FullyQualifiedName is not null:
          if (_types.TryGetValue(type.FullyQualifiedName, out var typeEntry))
          {
            assembly = typeEntry.Assembly;
          }
          break;
      }

      if (assembly is null || string.IsNullOrWhiteSpace(assembly.FullyQualifiedName))
      {
        return;
      }

      // Don't register files for excluded assemblies
      if (_assemblyFilter.ShouldExcludeAssembly(assembly.FullyQualifiedName))
      {
        return;
      }

      // Only register if assembly is actually in the solution (not excluded)
      if (!_assemblies.ContainsKey(assembly.FullyQualifiedName))
      {
        return;
      }

      _fileAssemblyMap[normalizedPath] = assembly;
    }

    private static MetricsNode? FindNodeInIndex(Dictionary<string, List<IndexedNode>> index, string path, int line)
    {
      if (!index.TryGetValue(path, out var list))
      {
        return null;
      }

      MetricsNode? bestNode = null;
      var bestLength = int.MaxValue;

      foreach (var node in list)
      {
        if (line < node.StartLine || line > node.EndLine)
        {
          continue;
        }

        var length = node.EndLine - node.StartLine;
        if (length < bestLength)
        {
          bestLength = length;
          bestNode = node.Node;
        }
      }

      return bestNode;
    }

    private static Dictionary<string, MetricsNode> CreateBaselineLookup(MetricsNode? baselineRoot)
    {
      var result = new Dictionary<string, MetricsNode>(StringComparer.Ordinal);
      if (baselineRoot is null)
      {
        return result;
      }

      TraverseBaseline(baselineRoot, baselineRoot.Name, result);
      return result;
    }

    private static void TraverseBaseline(MetricsNode node, string path, IDictionary<string, MetricsNode> lookup)
    {
      lookup[path] = node;

      foreach (var assembly in (node as SolutionMetricsNode)?.Assemblies ?? Array.Empty<AssemblyMetricsNode>())
      {
        TraverseBaseline(assembly, $"{path}/{assembly.Name}", lookup);
      }

      foreach (var ns in (node as AssemblyMetricsNode)?.Namespaces ?? Array.Empty<NamespaceMetricsNode>())
      {
        TraverseBaseline(ns, $"{path}/{ns.Name}", lookup);
      }

      foreach (var type in (node as NamespaceMetricsNode)?.Types ?? Array.Empty<TypeMetricsNode>())
      {
        TraverseBaseline(type, $"{path}/{type.Name}", lookup);
      }

      foreach (var member in (node as TypeMetricsNode)?.Members ?? Array.Empty<MemberMetricsNode>())
      {
        TraverseBaseline(member, $"{path}/{member.Name}", lookup);
      }
    }

    private void ApplyBaselineRecursive(
        MetricsNode node,
        IReadOnlyDictionary<string, MetricsNode> baselineLookup,
        IDictionary<MetricIdentifier, MetricThresholdDefinition> thresholds,
        string path)
    {
      baselineLookup.TryGetValue(path, out var baselineNode);

      if (node != Solution)
      {
        node.IsNew = baselineNode is null;
      }

      var symbolLevel = DetermineSymbolLevel(node);
      node.Metrics = ApplyMetricsBaseline(
          node.Metrics,
          baselineNode?.Metrics ?? new Dictionary<MetricIdentifier, MetricValue>(),
          thresholds,
          symbolLevel);

      switch (node)
      {
        case SolutionMetricsNode solution:
          foreach (var assembly in solution.Assemblies)
          {
            ApplyBaselineRecursive(assembly, baselineLookup, thresholds, $"{path}/{assembly.Name}");
          }

          break;
        case AssemblyMetricsNode assembly:
          foreach (var ns in assembly.Namespaces)
          {
            ApplyBaselineRecursive(ns, baselineLookup, thresholds, $"{path}/{ns.Name}");
          }

          break;
        case NamespaceMetricsNode @namespace:
          foreach (var type in @namespace.Types)
          {
            ApplyBaselineRecursive(type, baselineLookup, thresholds, $"{path}/{type.Name}");
          }

          break;
        case TypeMetricsNode type:
          foreach (var member in type.Members)
          {
            ApplyBaselineRecursive(member, baselineLookup, thresholds, $"{path}/{member.Name}");
          }

          break;
      }
    }

    private IDictionary<MetricIdentifier, MetricValue> ApplyMetricsBaseline(
        IDictionary<MetricIdentifier, MetricValue> metrics,
        IDictionary<MetricIdentifier, MetricValue> baselineMetrics,
        IDictionary<MetricIdentifier, MetricThresholdDefinition> thresholds,
        MetricSymbolLevel symbolLevel)
    {
      var result = new Dictionary<MetricIdentifier, MetricValue>();
      foreach (var identifier in Enum.GetValues<MetricIdentifier>())
      {
        metrics.TryGetValue(identifier, out var current);
        baselineMetrics.TryGetValue(identifier, out var baseline);

        var value = current?.Value;
        var delta = value.HasValue && baseline?.Value is decimal baselineValue
            ? value.Value - baselineValue
            : (decimal?)null;

        var status = EvaluateStatus(identifier, value, thresholds, symbolLevel);

        result[identifier] = new MetricValue
        {
          Value = value,
          Delta = delta,
          Unit = current?.Unit ?? baseline?.Unit,
          Status = status
        };
      }

      return result;
    }

    private static ThresholdStatus EvaluateStatus(
        MetricIdentifier identifier,
        decimal? value,
        IDictionary<MetricIdentifier, MetricThresholdDefinition> thresholds,
        MetricSymbolLevel symbolLevel)
    {
      if (!value.HasValue)
      {
        return ThresholdStatus.NotApplicable;
      }

      if (!thresholds.TryGetValue(identifier, out var definition))
      {
        return ThresholdStatus.NotApplicable;
      }

      var levels = definition.Levels;

      if (!levels.TryGetValue(symbolLevel, out var threshold))
      {
        if (!levels.TryGetValue(MetricSymbolLevel.Type, out threshold))
        {
          return ThresholdStatus.NotApplicable;
        }
      }

      if (!threshold.Warning.HasValue && !threshold.Error.HasValue)
      {
        return ThresholdStatus.NotApplicable;
      }

      var warning = threshold.Warning;
      var error = threshold.Error;

      if (threshold.HigherIsBetter)
      {
        if (error.HasValue && value <= error)
        {
          return ThresholdStatus.Error;
        }

        if (warning.HasValue && value <= warning)
        {
          return ThresholdStatus.Warning;
        }
      }
      else
      {
        if (error.HasValue && value >= error)
        {
          return ThresholdStatus.Error;
        }

        if (warning.HasValue && value >= warning)
        {
          return ThresholdStatus.Warning;
        }
      }

      return ThresholdStatus.Success;
    }

    private static MetricSymbolLevel DetermineSymbolLevel(MetricsNode node)
        => node switch
        {
          SolutionMetricsNode => MetricSymbolLevel.Solution,
          AssemblyMetricsNode => MetricSymbolLevel.Assembly,
          NamespaceMetricsNode => MetricSymbolLevel.Namespace,
          TypeMetricsNode => MetricSymbolLevel.Type,
          MemberMetricsNode => MetricSymbolLevel.Member,
          _ => MetricSymbolLevel.Member
        };

    private sealed record NamespaceEntry(NamespaceMetricsNode Node, AssemblyMetricsNode Assembly);

    private sealed record TypeEntry(TypeMetricsNode Node, AssemblyMetricsNode Assembly);

    private readonly record struct IndexedNode(MetricsNode Node, int StartLine, int EndLine);
  }
}

