namespace Rca.Tools.MetricsReporter.Aggregation;

using System;
using System.Collections.Generic;
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
    workspace.PrepareReport(input);
    
    // Collect rule IDs that are actually used in breakdown across all metrics
    var usedRuleIds = CollectUsedRuleIds(workspace.Solution);
    
    var metadataInput = ReportMetadataComposer.CreateInput(
        input,
        _memberFilter,
        _assemblyFilter,
        _typeFilter,
        usedRuleIds);
    var metadata = ReportMetadataComposer.Compose(metadataInput);

    return new MetricsReport
    {
      Metadata = metadata,
      Solution = workspace.Solution
    };
  }

  /// <summary>
  /// Recursively collects all rule IDs from breakdown dictionaries in SARIF metrics across the entire metrics tree.
  /// </summary>
  /// <param name="solution">The root solution node to traverse.</param>
  /// <returns>A set of rule IDs that are actually used in breakdown.</returns>
  private static HashSet<string> CollectUsedRuleIds(SolutionMetricsNode solution)
  {
    var usedRuleIds = new HashSet<string>(StringComparer.Ordinal);
    CollectUsedRuleIdsRecursive(solution, usedRuleIds);
    return usedRuleIds;
  }

  /// <summary>
  /// Recursively traverses the metrics tree and collects rule IDs from breakdown dictionaries.
  /// </summary>
  /// <param name="node">The current node to process.</param>
  /// <param name="usedRuleIds">The set to accumulate rule IDs into.</param>
  private static void CollectUsedRuleIdsRecursive(MetricsNode node, HashSet<string> usedRuleIds)
  {
    // Collect rule IDs from SARIF metrics breakdown
    if (node.Metrics.TryGetValue(MetricIdentifier.SarifCaRuleViolations, out var caMetric)
        && caMetric.Breakdown is not null)
    {
      foreach (var ruleId in caMetric.Breakdown.Keys)
      {
        usedRuleIds.Add(ruleId);
      }
    }

    if (node.Metrics.TryGetValue(MetricIdentifier.SarifIdeRuleViolations, out var ideMetric)
        && ideMetric.Breakdown is not null)
    {
      foreach (var ruleId in ideMetric.Breakdown.Keys)
      {
        usedRuleIds.Add(ruleId);
      }
    }

    // Recursively process child nodes
    if (node is SolutionMetricsNode solutionNode)
    {
      foreach (var assembly in solutionNode.Assemblies)
      {
        CollectUsedRuleIdsRecursive(assembly, usedRuleIds);
      }
    }
    else if (node is AssemblyMetricsNode assemblyNode)
    {
      foreach (var ns in assemblyNode.Namespaces)
      {
        CollectUsedRuleIdsRecursive(ns, usedRuleIds);
      }
    }
    else if (node is NamespaceMetricsNode namespaceNode)
    {
      foreach (var type in namespaceNode.Types)
      {
        CollectUsedRuleIdsRecursive(type, usedRuleIds);
      }
    }
    else if (node is TypeMetricsNode typeNode)
    {
      foreach (var member in typeNode.Members)
      {
        CollectUsedRuleIdsRecursive(member, usedRuleIds);
      }
    }
  }

  private sealed class AggregationWorkspace
  {
    private readonly AggregationWorkspaceState _state;
    private readonly AggregationWorkspaceWorkflow _workflow;

    public AggregationWorkspace(string solutionName, MemberFilter memberFilter, AssemblyFilter assemblyFilter, TypeFilter typeFilter)
    {
      _state = new AggregationWorkspaceState(solutionName);
      _workflow = CreateWorkflow(_state, memberFilter, assemblyFilter, typeFilter);
    }

    public SolutionMetricsNode Solution => _state.Solution;

    public void MergeStructuralElements(ParsedMetricsDocument document) => _workflow.MergeStructuralElements(document);

    public void ProcessDocuments(MetricsAggregationInput input) => _workflow.ProcessDocuments(input);

    public void BuildLineIndex() => _workflow.BuildLineIndex();

    public void PrepareReport(MetricsAggregationInput input) => _workflow.PrepareReport(input);

    public void ApplySarifDocument(ParsedMetricsDocument document) => _workflow.ApplySarifDocument(document);

    public void ApplyBaselineAndThresholds(
        MetricsReport? baseline,
        IDictionary<MetricIdentifier, MetricThresholdDefinition> thresholds)
        => _workflow.ApplyBaselineAndThresholds(baseline, thresholds);

    public void ReconcileIteratorStateMachineMetrics() => _workflow.ReconcileIteratorStateMachineMetrics();

    public void ReconcilePlainNestedTypeMetrics() => _workflow.ReconcilePlainNestedTypeMetrics();

    private static AggregationWorkspaceWorkflow CreateWorkflow(
        AggregationWorkspaceState state,
        MemberFilter memberFilter,
        AssemblyFilter assemblyFilter,
        TypeFilter typeFilter)
    {
      var documentProcessor = new AggregationDocumentProcessor(state, memberFilter, assemblyFilter, typeFilter);
      var lineIndexProcessor = new AggregationLineIndexProcessor(state, assemblyFilter);
      var sarifProcessor = new AggregationSarifProcessor(state, assemblyFilter);
      var baselineProcessor = new AggregationBaselineAndThresholdProcessor(state);
      var reconciliationProcessor = new AggregationReconciliationProcessor(state);

      return new AggregationWorkspaceWorkflow(
          state,
          documentProcessor,
          lineIndexProcessor,
          sarifProcessor,
          baselineProcessor,
          reconciliationProcessor);
    }
  }

  private sealed class AggregationWorkspaceState
  {
    public AggregationWorkspaceState(string solutionName)
    {
      Solution = new SolutionMetricsNode
      {
        Name = solutionName,
        FullyQualifiedName = solutionName,
        Metrics = new Dictionary<MetricIdentifier, MetricValue>()
      };
      Assemblies = new Dictionary<string, AssemblyMetricsNode>(StringComparer.OrdinalIgnoreCase);
      Namespaces = new Dictionary<string, NamespaceEntry>(StringComparer.Ordinal);
      NamespaceIndex = new Dictionary<string, List<NamespaceEntry>>(StringComparer.Ordinal);
      Types = new Dictionary<string, TypeEntry>(StringComparer.Ordinal);
      Members = new Dictionary<string, MemberMetricsNode>(StringComparer.Ordinal);
      LineIndex = new LineIndex();
    }

    public SolutionMetricsNode Solution { get; }

    public Dictionary<string, AssemblyMetricsNode> Assemblies { get; }

    public Dictionary<string, NamespaceEntry> Namespaces { get; }

    public Dictionary<string, List<NamespaceEntry>> NamespaceIndex { get; }

    public Dictionary<string, TypeEntry> Types { get; }

    public Dictionary<string, MemberMetricsNode> Members { get; }

    public LineIndex LineIndex { get; }
  }

  private interface IAggregationDocumentProcessor
  {
    void MergeStructuralElements(ParsedMetricsDocument document);
  }

  private interface IAggregationLineIndexProcessor
  {
    void BuildLineIndex();
  }

  private interface IAggregationSarifProcessor
  {
    void ApplySarifDocument(ParsedMetricsDocument document);
  }

  private interface IAggregationBaselineAndThresholdProcessor
  {
    void ApplyBaselineAndThresholds(
        MetricsReport? baseline,
        IDictionary<MetricIdentifier, MetricThresholdDefinition> thresholds);
  }

  private interface IAggregationReconciliationProcessor
  {
    void ReconcileIteratorStateMachineMetrics();

    void ReconcilePlainNestedTypeMetrics();
  }

  private sealed class AggregationDocumentProcessor : IAggregationDocumentProcessor
  {
    private readonly StructuralElementMerger _structuralMerger;

    public AggregationDocumentProcessor(
        AggregationWorkspaceState state,
        MemberFilter memberFilter,
        AssemblyFilter assemblyFilter,
        TypeFilter typeFilter)
    {
      ArgumentNullException.ThrowIfNull(state);
      ArgumentNullException.ThrowIfNull(memberFilter);
      ArgumentNullException.ThrowIfNull(assemblyFilter);
      ArgumentNullException.ThrowIfNull(typeFilter);

      _structuralMerger = new StructuralElementMerger(
          state.Solution,
          state.Assemblies,
          state.Namespaces,
          state.NamespaceIndex,
          state.Types,
          state.Members,
          memberFilter,
          assemblyFilter,
          typeFilter);
    }

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
            _structuralMerger.MergeAssembly(element);
            break;
          case CodeElementKind.Namespace:
            _structuralMerger.MergeNamespace(element);
            break;
          case CodeElementKind.Type:
            _structuralMerger.MergeType(element);
            break;
          case CodeElementKind.Member:
            _structuralMerger.MergeMember(element);
            break;
          default:
            break;
        }
      }
    }
  }

  private sealed class AggregationLineIndexProcessor : IAggregationLineIndexProcessor
  {
    private readonly AggregationWorkspaceState _state;
    private readonly AggregationWorkspaceLookup _lookup;

    public AggregationLineIndexProcessor(
        AggregationWorkspaceState state,
        AssemblyFilter assemblyFilter)
    {
      _state = state ?? throw new ArgumentNullException(nameof(state));
      ArgumentNullException.ThrowIfNull(assemblyFilter);

      _lookup = new AggregationWorkspaceLookup(
          _state.Assemblies,
          _state.NamespaceIndex,
          _state.Types,
          assemblyFilter);
    }

    public void BuildLineIndex()
    {
      TypeSourceBackfiller.PopulateMissingSources(_state.Types.Values);
      LineIndexBuilder.Build(_state.LineIndex, _state.Members.Values, _state.Types.Values, _lookup);
    }
  }

  private sealed class AggregationSarifProcessor : IAggregationSarifProcessor
  {
    private readonly AggregationWorkspaceState _state;
    private readonly SarifMetricsApplier _sarifApplier;

    public AggregationSarifProcessor(
        AggregationWorkspaceState state,
        AssemblyFilter assemblyFilter)
    {
      _state = state ?? throw new ArgumentNullException(nameof(state));
      ArgumentNullException.ThrowIfNull(assemblyFilter);

      _sarifApplier = new SarifMetricsApplier(
          _state.LineIndex,
          assemblyFilter,
          (node, identifier, value) => MergeMetric(node, identifier, value, aggregate: true));
    }

    public void ApplySarifDocument(ParsedMetricsDocument document)
        => _sarifApplier.Apply(document, _state.Solution);
  }

  private sealed class AggregationBaselineAndThresholdProcessor : IAggregationBaselineAndThresholdProcessor
  {
    private readonly AggregationWorkspaceState _state;
    private readonly BaselineEvaluator _baselineEvaluator;

    public AggregationBaselineAndThresholdProcessor(AggregationWorkspaceState state)
    {
      _state = state ?? throw new ArgumentNullException(nameof(state));
      _baselineEvaluator = new BaselineEvaluator();
    }

    public void ApplyBaselineAndThresholds(
        MetricsReport? baseline,
        IDictionary<MetricIdentifier, MetricThresholdDefinition> thresholds)
        => _baselineEvaluator.Apply(_state.Solution, baseline?.Solution, thresholds);
  }

  private sealed class AggregationReconciliationProcessor : IAggregationReconciliationProcessor
  {
    private readonly AggregationWorkspaceState _state;

    public AggregationReconciliationProcessor(AggregationWorkspaceState state)
    {
      _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public void ReconcileIteratorStateMachineMetrics()
        => IteratorCoverageReconciler.Reconcile(_state.Types, RemoveIteratorTypeFromHierarchy);

    public void ReconcilePlainNestedTypeMetrics()
        => PlainNestedTypeCoverageReconciler.Reconcile(
            _state.Types,
            _state.Members,
            RemoveIteratorTypeFromHierarchy);

    private void RemoveIteratorTypeFromHierarchy(string iteratorTypeKey, TypeEntry iteratorTypeEntry)
    {
      _state.Types.Remove(iteratorTypeKey);

      var iteratorTypeNode = iteratorTypeEntry.Node;
      var assembly = iteratorTypeEntry.Assembly;

      foreach (var ns in assembly.Namespaces)
      {
        if (ns.Types.Remove(iteratorTypeNode))
        {
          break;
        }
      }
    }
  }

  private sealed class AggregationWorkspaceWorkflow
  {
    private readonly AggregationWorkspaceState _state;
    private readonly IAggregationDocumentProcessor _documentProcessor;
    private readonly IAggregationLineIndexProcessor _lineIndexProcessor;
    private readonly IAggregationSarifProcessor _sarifProcessor;
    private readonly IAggregationBaselineAndThresholdProcessor _baselineProcessor;
    private readonly IAggregationReconciliationProcessor _reconciliationProcessor;

    public AggregationWorkspaceWorkflow(
        AggregationWorkspaceState state,
        IAggregationDocumentProcessor documentProcessor,
        IAggregationLineIndexProcessor lineIndexProcessor,
        IAggregationSarifProcessor sarifProcessor,
        IAggregationBaselineAndThresholdProcessor baselineProcessor,
        IAggregationReconciliationProcessor reconciliationProcessor)
    {
      _state = state ?? throw new ArgumentNullException(nameof(state));
      _documentProcessor = documentProcessor ?? throw new ArgumentNullException(nameof(documentProcessor));
      _lineIndexProcessor = lineIndexProcessor ?? throw new ArgumentNullException(nameof(lineIndexProcessor));
      _sarifProcessor = sarifProcessor ?? throw new ArgumentNullException(nameof(sarifProcessor));
      _baselineProcessor = baselineProcessor ?? throw new ArgumentNullException(nameof(baselineProcessor));
      _reconciliationProcessor = reconciliationProcessor ?? throw new ArgumentNullException(nameof(reconciliationProcessor));
    }

    public void MergeStructuralElements(ParsedMetricsDocument document)
        => _documentProcessor.MergeStructuralElements(document);

    public void ProcessDocuments(MetricsAggregationInput input)
    {
      ArgumentNullException.ThrowIfNull(input);

      foreach (var document in input.AltCoverDocuments)
      {
        _documentProcessor.MergeStructuralElements(document);
      }

      foreach (var document in input.RoslynDocuments)
      {
        _documentProcessor.MergeStructuralElements(document);
      }

      _lineIndexProcessor.BuildLineIndex();

      foreach (var document in input.SarifDocuments)
      {
        _sarifProcessor.ApplySarifDocument(document);
      }
    }

    public void PrepareReport(MetricsAggregationInput input)
    {
      ArgumentNullException.ThrowIfNull(input);

      ProcessDocuments(input);
      _reconciliationProcessor.ReconcileIteratorStateMachineMetrics();
      _reconciliationProcessor.ReconcilePlainNestedTypeMetrics();
      _baselineProcessor.ApplyBaselineAndThresholds(input.Baseline, input.Thresholds);
    }

    public void BuildLineIndex()
        => _lineIndexProcessor.BuildLineIndex();

    public void ApplySarifDocument(ParsedMetricsDocument document)
        => _sarifProcessor.ApplySarifDocument(document);

    public void ApplyBaselineAndThresholds(
        MetricsReport? baseline,
        IDictionary<MetricIdentifier, MetricThresholdDefinition> thresholds)
        => _baselineProcessor.ApplyBaselineAndThresholds(baseline, thresholds);

    public void ReconcileIteratorStateMachineMetrics()
        => _reconciliationProcessor.ReconcileIteratorStateMachineMetrics();

    public void ReconcilePlainNestedTypeMetrics()
        => _reconciliationProcessor.ReconcilePlainNestedTypeMetrics();
  }

  private static void MergeMetric(MetricsNode node, MetricIdentifier identifier, MetricValue value, bool aggregate)
  {
    if (!node.Metrics.TryGetValue(identifier, out var existing))
    {
      // WHY: When adding a metric for the first time, we preserve the breakdown if present.
      // This ensures that SARIF metrics with breakdown information are correctly stored
      // even on the first assignment, which is important for metrics applied via LineIndex.
      // We create a new MetricValue to ensure the breakdown dictionary is properly copied.
      node.Metrics[identifier] = new MetricValue
      {
        Value = value.Value,
        Delta = value.Delta,
        Status = value.Status,
        Unit = value.Unit,
        Breakdown = value.Breakdown is not null && value.Breakdown.Count > 0
            ? new Dictionary<string, int>(value.Breakdown)
            : null
      };
      return;
    }

    if (aggregate && value.Value.HasValue)
    {
      var sum = (existing.Value ?? 0m) + value.Value.Value;
      
      // WHY: We merge breakdown dictionaries when aggregating SARIF metrics to preserve
      // the detailed breakdown of rule violations. This allows the report to show which
      // specific rules are violated at each level of the hierarchy (Member, Type, etc.).
      // If both values have breakdowns, we sum the counts for each rule ID.
      var mergedBreakdown = MergeBreakdown(existing.Breakdown, value.Breakdown);
      
      node.Metrics[identifier] = new MetricValue
      {
        Value = sum,
        Unit = existing.Unit ?? value.Unit,
        Status = ThresholdStatus.NotApplicable,
        Breakdown = mergedBreakdown
      };
    }
    else if (!existing.Value.HasValue && value.Value.HasValue)
    {
      // WHY: When replacing a null value with a real value, we preserve the breakdown
      // from the incoming value to ensure SARIF breakdown information is not lost.
      // We create a new MetricValue to ensure the breakdown dictionary is properly copied.
      node.Metrics[identifier] = new MetricValue
      {
        Value = value.Value,
        Delta = value.Delta,
        Status = value.Status,
        Unit = value.Unit,
        Breakdown = value.Breakdown is not null && value.Breakdown.Count > 0
            ? new Dictionary<string, int>(value.Breakdown)
            : null
      };
    }
  }

  /// <summary>
  /// Merges two breakdown dictionaries by summing counts for matching rule IDs.
  /// </summary>
  /// <param name="existing">The existing breakdown dictionary, may be <see langword="null"/>.</param>
  /// <param name="incoming">The incoming breakdown dictionary to merge, may be <see langword="null"/>.</param>
  /// <returns>
  /// A merged breakdown dictionary, or <see langword="null"/> if both inputs are <see langword="null"/> or empty.
  /// </returns>
  private static Dictionary<string, int>? MergeBreakdown(Dictionary<string, int>? existing, Dictionary<string, int>? incoming)
  {
    if (incoming is null || incoming.Count == 0)
    {
      return existing;
    }

    if (existing is null || existing.Count == 0)
    {
      return incoming;
    }

    // WHY: We create a new dictionary to avoid mutating the existing one, which may be shared
    // across multiple nodes in the hierarchy. We sum counts for matching rule IDs and preserve
    // all unique rule IDs from both dictionaries.
    var merged = new Dictionary<string, int>(existing);
    foreach (var pair in incoming)
    {
      merged.TryGetValue(pair.Key, out var existingCount);
      merged[pair.Key] = existingCount + pair.Value;
    }

    return merged;
  }

  private static class ReportMetadataComposer
  {
    public static ReportMetadata Compose(ReportMetadataInput input)
    {
      ArgumentNullException.ThrowIfNull(input);

      return new ReportMetadata
      {
        GeneratedAtUtc = DateTime.UtcNow,
        BaselineReference = input.BaselineReference,
        Paths = input.Paths,
        ThresholdsByLevel = input.ThresholdMetadata.ThresholdsByLevel,
        ThresholdDescriptions = input.ThresholdMetadata.Descriptions,
        ExcludedMemberNamesPatterns = input.ExcludedMemberNamesPatterns,
        ExcludedAssemblyNames = input.ExcludedAssemblyNames,
        ExcludedTypeNamePatterns = input.ExcludedTypeNamePatterns,
        SuppressedSymbols = input.SuppressedSymbols,
        RuleDescriptions = input.RuleDescriptions
      };
    }

    public static ReportMetadataInput CreateInput(
        MetricsAggregationInput input,
        MemberFilter memberFilter,
        AssemblyFilter assemblyFilter,
        TypeFilter typeFilter,
        HashSet<string>? usedRuleIds = null)
    {
      ArgumentNullException.ThrowIfNull(input);
      ArgumentNullException.ThrowIfNull(memberFilter);
      ArgumentNullException.ThrowIfNull(assemblyFilter);
      ArgumentNullException.ThrowIfNull(typeFilter);

      var thresholdMetadata = CreateThresholdMetadata(input.Thresholds);
      var allRuleDescriptions = MergeRuleDescriptions(input.SarifDocuments);
      
      // Filter rule descriptions to only include rules that are actually used in breakdown
      var ruleDescriptions = usedRuleIds is not null
          ? FilterRuleDescriptions(allRuleDescriptions, usedRuleIds)
          : allRuleDescriptions;

      return new ReportMetadataInput(
          input.BaselineReference,
          input.Paths,
          thresholdMetadata,
          memberFilter.GetExcludedMemberNamesPatternsString(),
          assemblyFilter.GetExcludedAssemblyPatternsString(),
          typeFilter.GetExcludedTypePatternsString(),
          input.SuppressedSymbols,
          ruleDescriptions);
    }

    /// <summary>
    /// Filters rule descriptions to only include rules that are actually used in breakdown.
    /// </summary>
    /// <param name="allRuleDescriptions">All rule descriptions from SARIF files.</param>
    /// <param name="usedRuleIds">Set of rule IDs that are actually used in breakdown.</param>
    /// <returns>A filtered dictionary containing only used rule descriptions.</returns>
    private static Dictionary<string, RuleDescription> FilterRuleDescriptions(
        Dictionary<string, RuleDescription> allRuleDescriptions,
        HashSet<string> usedRuleIds)
    {
      var filtered = new Dictionary<string, RuleDescription>();
      
      foreach (var (ruleId, description) in allRuleDescriptions)
      {
        if (usedRuleIds.Contains(ruleId))
        {
          filtered[ruleId] = description;
        }
      }
      
      return filtered;
    }

    /// <summary>
    /// Merges rule descriptions from all SARIF documents, detecting and warning about conflicts.
    /// </summary>
    /// <param name="sarifDocuments">The SARIF documents to merge rule descriptions from.</param>
    /// <returns>A dictionary of merged rule descriptions keyed by rule ID.</returns>
    private static Dictionary<string, RuleDescription> MergeRuleDescriptions(IList<ParsedMetricsDocument> sarifDocuments)
    {
      var merged = new Dictionary<string, RuleDescription>();

      foreach (var document in sarifDocuments)
      {
        foreach (var (ruleId, description) in document.RuleDescriptions)
        {
          if (merged.TryGetValue(ruleId, out var existing))
          {
            // Check for differences and warn if found
            if (!AreRuleDescriptionsEqual(existing, description))
            {
              Console.Error.WriteLine(
                  $"WARNING: Rule {ruleId} has different descriptions across SARIF files. " +
                  $"Using first encountered description. " +
                  $"Existing: Short='{existing.ShortDescription}', " +
                  $"Incoming: Short='{description.ShortDescription}'");
            }
          }
          else
          {
            merged[ruleId] = description;
          }
        }
      }

      return merged;
    }

    /// <summary>
    /// Compares two rule descriptions for equality.
    /// </summary>
    /// <param name="first">The first rule description.</param>
    /// <param name="second">The second rule description.</param>
    /// <returns><see langword="true"/> if the descriptions are equal; otherwise, <see langword="false"/>.</returns>
    private static bool AreRuleDescriptionsEqual(RuleDescription first, RuleDescription second)
    {
      return string.Equals(first.ShortDescription, second.ShortDescription, StringComparison.Ordinal)
          && string.Equals(first.FullDescription ?? string.Empty, second.FullDescription ?? string.Empty, StringComparison.Ordinal)
          && string.Equals(first.HelpUri ?? string.Empty, second.HelpUri ?? string.Empty, StringComparison.Ordinal)
          && string.Equals(first.Category ?? string.Empty, second.Category ?? string.Empty, StringComparison.Ordinal);
    }

    private static ReportThresholdMetadata CreateThresholdMetadata(
        IDictionary<MetricIdentifier, MetricThresholdDefinition> thresholds)
    {
      ArgumentNullException.ThrowIfNull(thresholds);

      var (thresholdLevels, thresholdDescriptions) = ThresholdMetadataBuilder.Build(thresholds);
      return new ReportThresholdMetadata(thresholdLevels, thresholdDescriptions);
    }
  }

  private sealed record ReportMetadataInput(
      string? BaselineReference,
      ReportPaths Paths,
      ReportThresholdMetadata ThresholdMetadata,
      string? ExcludedMemberNamesPatterns,
      string? ExcludedAssemblyNames,
      string? ExcludedTypeNamePatterns,
      IList<SuppressedSymbolInfo> SuppressedSymbols,
      Dictionary<string, RuleDescription> RuleDescriptions);

  private sealed class ReportThresholdMetadata
  {
    private readonly Dictionary<MetricIdentifier, IDictionary<MetricSymbolLevel, MetricThreshold>> _thresholdsByLevel;
    private readonly Dictionary<MetricIdentifier, string?> _descriptions;

    public ReportThresholdMetadata(
        Dictionary<MetricIdentifier, IDictionary<MetricSymbolLevel, MetricThreshold>> thresholdsByLevel,
        Dictionary<MetricIdentifier, string?> descriptions)
    {
      _thresholdsByLevel = thresholdsByLevel ?? throw new ArgumentNullException(nameof(thresholdsByLevel));
      _descriptions = descriptions ?? throw new ArgumentNullException(nameof(descriptions));
    }

    public Dictionary<MetricIdentifier, IDictionary<MetricSymbolLevel, MetricThreshold>> ThresholdsByLevel
        => _thresholdsByLevel;

    public Dictionary<MetricIdentifier, string?> Descriptions
        => _descriptions;
  }
}
