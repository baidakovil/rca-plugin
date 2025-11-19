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
    var metadataInput = ReportMetadataComposer.CreateInput(
        input,
        _memberFilter,
        _assemblyFilter,
        _typeFilter);
    var metadata = ReportMetadataComposer.Compose(metadataInput);

    return new MetricsReport
    {
      Metadata = metadata,
      Solution = workspace.Solution
    };
  }

  private sealed class AggregationWorkspace
  {
    private readonly AggregationWorkspaceCore _core;

    public AggregationWorkspace(string solutionName, MemberFilter memberFilter, AssemblyFilter assemblyFilter, TypeFilter typeFilter)
    {
      _core = new AggregationWorkspaceCore(solutionName, memberFilter, assemblyFilter, typeFilter);
    }

    public SolutionMetricsNode Solution => _core.Solution;

    public void MergeStructuralElements(ParsedMetricsDocument document) => _core.MergeStructuralElements(document);

    public void ProcessDocuments(MetricsAggregationInput input) => _core.ProcessDocuments(input);

    public void BuildLineIndex() => _core.BuildLineIndex();

    public void PrepareReport(MetricsAggregationInput input) => _core.PrepareReport(input);

    public void ApplySarifDocument(ParsedMetricsDocument document) => _core.ApplySarifDocument(document);

    public void ApplyBaselineAndThresholds(
        MetricsReport? baseline,
        IDictionary<MetricIdentifier, MetricThresholdDefinition> thresholds)
        => _core.ApplyBaselineAndThresholds(baseline, thresholds);

    public void ReconcileIteratorStateMachineMetrics() => _core.ReconcileIteratorStateMachineMetrics();

    public void ReconcilePlainNestedTypeMetrics() => _core.ReconcilePlainNestedTypeMetrics();
  }

  private sealed class AggregationWorkspaceCore
  {
    private readonly AggregationWorkspaceState _state;
    private readonly AggregationWorkspaceWorkflow _workflow;

    public AggregationWorkspaceCore(string solutionName, MemberFilter memberFilter, AssemblyFilter assemblyFilter, TypeFilter typeFilter)
    {
      _state = new AggregationWorkspaceState(solutionName);
      _workflow = new AggregationWorkspaceWorkflow(_state, memberFilter, assemblyFilter, typeFilter);
    }

    public SolutionMetricsNode Solution => _state.Solution;

    public void MergeStructuralElements(ParsedMetricsDocument document) => _workflow.MergeStructuralElements(document);

    public void ProcessDocuments(MetricsAggregationInput input) => _workflow.ProcessDocuments(input);

    public void PrepareReport(MetricsAggregationInput input) => _workflow.PrepareReport(input);

    public void BuildLineIndex() => _workflow.BuildLineIndex();

    public void ApplySarifDocument(ParsedMetricsDocument document) => _workflow.ApplySarifDocument(document);

    public void ApplyBaselineAndThresholds(MetricsReport? baseline, IDictionary<MetricIdentifier, MetricThresholdDefinition> thresholds)
        => _workflow.ApplyBaselineAndThresholds(baseline, thresholds);

    public void ReconcileIteratorStateMachineMetrics() => _workflow.ReconcileIteratorStateMachineMetrics();

    public void ReconcilePlainNestedTypeMetrics() => _workflow.ReconcilePlainNestedTypeMetrics();
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

  private sealed class AggregationWorkspaceWorkflow
  {
    private readonly AggregationWorkspaceState _state;
    private readonly MemberFilter _memberFilter;
    private readonly AssemblyFilter _assemblyFilter;
    private readonly TypeFilter _typeFilter;
    private readonly BaselineEvaluator _baselineEvaluator;
    private readonly IteratorCoverageReconciler _iteratorReconciler;
    private readonly PlainNestedTypeCoverageReconciler _plainNestedTypeCoverageReconciler;
    private readonly StructuralElementMerger _structuralMerger;
    private readonly AggregationWorkspaceLookup _lookup;
    private readonly SarifMetricsApplier _sarifApplier;
    private readonly LineIndexBuilder _lineIndexBuilder;

    public AggregationWorkspaceWorkflow(
        AggregationWorkspaceState state,
        MemberFilter memberFilter,
        AssemblyFilter assemblyFilter,
        TypeFilter typeFilter)
    {
      _state = state ?? throw new ArgumentNullException(nameof(state));
      _memberFilter = memberFilter;
      _assemblyFilter = assemblyFilter;
      _typeFilter = typeFilter;
      _baselineEvaluator = new BaselineEvaluator();
      _iteratorReconciler = new IteratorCoverageReconciler();
      _plainNestedTypeCoverageReconciler = new PlainNestedTypeCoverageReconciler();
      _structuralMerger = new StructuralElementMerger(
          _state.Solution,
          _state.Assemblies,
          _state.Namespaces,
          _state.NamespaceIndex,
          _state.Types,
          _state.Members,
          _memberFilter,
          _assemblyFilter,
          _typeFilter);
      _lookup = new AggregationWorkspaceLookup(
          _state.Assemblies,
          _state.NamespaceIndex,
          _state.Types,
          _assemblyFilter);
      _sarifApplier = new SarifMetricsApplier(
          _state.LineIndex,
          _assemblyFilter,
          (node, identifier, value) => MergeMetric(node, identifier, value, aggregate: true));
      _lineIndexBuilder = new LineIndexBuilder();
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

    public void ProcessDocuments(MetricsAggregationInput input)
    {
      ArgumentNullException.ThrowIfNull(input);

      foreach (var document in input.AltCoverDocuments)
      {
        MergeStructuralElements(document);
      }

      foreach (var document in input.RoslynDocuments)
      {
        MergeStructuralElements(document);
      }

      BuildLineIndex();

      foreach (var document in input.SarifDocuments)
      {
        ApplySarifDocument(document);
      }
    }

    public void PrepareReport(MetricsAggregationInput input)
    {
      ArgumentNullException.ThrowIfNull(input);

      ProcessDocuments(input);
      ReconcileIteratorStateMachineMetrics();
      ReconcilePlainNestedTypeMetrics();
      ApplyBaselineAndThresholds(input.Baseline, input.Thresholds);
    }

    public void BuildLineIndex()
        => _lineIndexBuilder.Build(_state.LineIndex, _state.Members.Values, _state.Types.Values, _lookup);

    public void ApplySarifDocument(ParsedMetricsDocument document)
        => _sarifApplier.Apply(document, _state.Solution);

    public void ApplyBaselineAndThresholds(
        MetricsReport? baseline,
        IDictionary<MetricIdentifier, MetricThresholdDefinition> thresholds)
        => _baselineEvaluator.Apply(_state.Solution, baseline?.Solution, thresholds);

    public void ReconcileIteratorStateMachineMetrics()
        => _iteratorReconciler.Reconcile(_state.Types, RemoveIteratorTypeFromHierarchy);

    public void ReconcilePlainNestedTypeMetrics()
        => _plainNestedTypeCoverageReconciler.Reconcile(
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

    private static void MergeMetric(MetricsNode node, MetricIdentifier identifier, MetricValue value, bool aggregate)
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
  }

  private interface IReportThresholdMetadata
  {
    ReportMetadata CreateMetadata(
        string? baselineReference,
        ReportPaths paths,
        string? excludedMemberNamesPatterns,
        string? excludedAssemblyNames,
        string? excludedTypeNamePatterns);
  }

  private static class ReportMetadataComposer
  {
    public static ReportMetadata Compose(ReportMetadataInput input)
    {
      ArgumentNullException.ThrowIfNull(input);

      return input.ThresholdMetadata.CreateMetadata(
          input.BaselineReference,
          input.Paths,
          input.ExcludedMemberNamesPatterns,
          input.ExcludedAssemblyNames,
          input.ExcludedTypeNamePatterns);
    }

    public static ReportMetadataInput CreateInput(
        MetricsAggregationInput input,
        MemberFilter memberFilter,
        AssemblyFilter assemblyFilter,
        TypeFilter typeFilter)
    {
      ArgumentNullException.ThrowIfNull(input);
      ArgumentNullException.ThrowIfNull(memberFilter);
      ArgumentNullException.ThrowIfNull(assemblyFilter);
      ArgumentNullException.ThrowIfNull(typeFilter);

      var (thresholdLevels, thresholdDescriptions) = ThresholdMetadataBuilder.Build(input.Thresholds);
      var thresholdMetadata = new ReportThresholdMetadata(thresholdLevels, thresholdDescriptions);

      return new ReportMetadataInput(
          input.BaselineReference,
          input.Paths,
          thresholdMetadata,
          memberFilter.GetExcludedMemberNamesPatternsString(),
          assemblyFilter.GetExcludedAssemblyPatternsString(),
          typeFilter.GetExcludedTypePatternsString());
    }
  }

  private sealed record ReportMetadataInput(
      string? BaselineReference,
      ReportPaths Paths,
      IReportThresholdMetadata ThresholdMetadata,
      string? ExcludedMemberNamesPatterns,
      string? ExcludedAssemblyNames,
      string? ExcludedTypeNamePatterns);

  private sealed class ReportThresholdMetadata : IReportThresholdMetadata
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

    public ReportMetadata CreateMetadata(
        string? baselineReference,
        ReportPaths paths,
        string? excludedMemberNamesPatterns,
        string? excludedAssemblyNames,
        string? excludedTypeNamePatterns)
        => new()
        {
          GeneratedAtUtc = DateTime.UtcNow,
          BaselineReference = baselineReference,
          Paths = paths,
          ThresholdsByLevel = _thresholdsByLevel,
          ThresholdDescriptions = _descriptions,
          ExcludedMemberNamesPatterns = excludedMemberNamesPatterns,
          ExcludedAssemblyNames = excludedAssemblyNames,
          ExcludedTypeNamePatterns = excludedTypeNamePatterns
        };
  }
}
