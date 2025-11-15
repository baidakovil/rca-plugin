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

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricsAggregationService"/> class with default filters.
    /// </summary>
    public MetricsAggregationService()
        : this(new MemberFilter(), new AssemblyFilter())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricsAggregationService"/> class with the specified filters.
    /// </summary>
    /// <param name="memberFilter">The member filter to use for excluding methods. Cannot be null.</param>
    /// <param name="assemblyFilter">The assembly filter to use for excluding assemblies. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="memberFilter"/> or <paramref name="assemblyFilter"/> is null.</exception>
    public MetricsAggregationService(MemberFilter memberFilter, AssemblyFilter assemblyFilter)
    {
        ArgumentNullException.ThrowIfNull(memberFilter);
        ArgumentNullException.ThrowIfNull(assemblyFilter);
        _memberFilter = memberFilter;
        _assemblyFilter = assemblyFilter;
    }

    /// <summary>
    /// Creates the final metrics report.
    /// </summary>
    /// <param name="input">Aggregation input data.</param>
    /// <returns>Composed metrics report.</returns>
    public MetricsReport BuildReport(MetricsAggregationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var workspace = new AggregationWorkspace(input.SolutionName, _memberFilter, _assemblyFilter);

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

        workspace.ApplyBaselineAndThresholds(input.Baseline, input.Thresholds);

        var (thresholdLevels, thresholdDescriptions) = CreateMetadataThresholds(input.Thresholds);

        var metadata = new ReportMetadata
        {
            GeneratedAtUtc = DateTime.UtcNow,
            BaselineReference = input.BaselineReference,
            Paths = input.Paths,
            ThresholdsByLevel = thresholdLevels,
            ThresholdDescriptions = thresholdDescriptions,
            ExcludedMethodNames = _memberFilter.GetExcludedMethodNamesString(),
            ExcludedAssemblyNames = _assemblyFilter.GetExcludedAssemblyPatternsString()
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
            HigherIsBetter = threshold.HigherIsBetter
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

        public AggregationWorkspace(string solutionName, MemberFilter memberFilter, AssemblyFilter assemblyFilter)
        {
            Solution = new SolutionMetricsNode
            {
                Name = solutionName,
                FullyQualifiedName = solutionName,
                Metrics = new Dictionary<MetricIdentifier, MetricValue>()
            };
            _memberFilter = memberFilter;
            _assemblyFilter = assemblyFilter;
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

