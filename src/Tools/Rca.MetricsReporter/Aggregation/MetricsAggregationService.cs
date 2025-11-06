namespace Rca.Tools.MetricsReporter.Aggregation;

using System;
using System.Collections.Generic;
using System.Linq;
using Rca.Tools.MetricsReporter.Model;
using Rca.Tools.MetricsReporter.Processing;

/// <summary>
/// Собирает итоговый отчёт на основе разобранных источников метрик.
/// </summary>
public sealed class MetricsAggregationService
{
    /// <summary>
    /// Строит итоговый отчёт по метрикам.
    /// </summary>
    /// <param name="input">Входные данные агрегации.</param>
    /// <returns>Сформированный отчёт.</returns>
    public MetricsReport BuildReport(MetricsAggregationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var workspace = new AggregationWorkspace(input.SolutionName);

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

        var metadata = new ReportMetadata
        {
            GeneratedAtUtc = DateTime.UtcNow,
            BaselineReference = input.BaselineReference,
            Paths = input.Paths,
            Thresholds = new Dictionary<MetricIdentifier, MetricThreshold>(input.Thresholds)
        };

        return new MetricsReport
        {
            Metadata = metadata,
            Solution = workspace.Solution
        };
    }

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

        public AggregationWorkspace(string solutionName)
        {
            Solution = new SolutionMetricsNode
            {
                Name = solutionName,
                FullyQualifiedName = solutionName,
                Metrics = new Dictionary<MetricIdentifier, MetricValue>()
            };
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

        public void BuildLineIndex()
        {
            foreach (var member in _members.Values)
            {
                if (member.Source?.Path is null)
                {
                    continue;
                }

                if (!member.Source.StartLine.HasValue)
                {
                    continue;
                }

                var start = member.Source.StartLine.Value;
                var end = member.Source.EndLine ?? start;
                var normalizedPath = NormalizePath(member.Source.Path);

                if (!_memberLineIndex.TryGetValue(normalizedPath, out var list))
                {
                    list = new List<IndexedNode>();
                    _memberLineIndex[normalizedPath] = list;
                }

                list.Add(new IndexedNode(member, start, end));
                RegisterFileAssembly(normalizedPath, member);
            }

            foreach (var typeEntry in _types.Values)
            {
                var type = typeEntry.Node;
                if (type.Source?.Path is null || type.Source.StartLine is null)
                {
                    continue;
                }

                var start = type.Source.StartLine.Value;
                var end = type.Source.EndLine ?? start;
                var normalizedPath = NormalizePath(type.Source.Path);

                if (!_typeLineIndex.TryGetValue(normalizedPath, out var list))
                {
                    list = new List<IndexedNode>();
                    _typeLineIndex[normalizedPath] = list;
                }

                list.Add(new IndexedNode(type, start, end));
                RegisterFileAssembly(normalizedPath, typeEntry.Assembly);
            }

            foreach (var list in _memberLineIndex.Values)
            {
                list.Sort(static (a, b) => a.StartLine.CompareTo(b.StartLine));
            }

            foreach (var list in _typeLineIndex.Values)
            {
                list.Sort(static (a, b) => a.StartLine.CompareTo(b.StartLine));
            }
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
                    target = assembly;
                }

                target ??= Solution;

                MergeMetric(target, metric.Key, metric.Value, aggregate: true);
            }
        }

        public void ApplyBaselineAndThresholds(MetricsReport? baseline, IDictionary<MetricIdentifier, MetricThreshold> thresholds)
        {
            var baselineLookup = CreateBaselineLookup(baseline?.Solution);
            ApplyBaselineRecursive(Solution, baselineLookup, thresholds, Solution.Name);
        }

        private void MergeAssembly(ParsedCodeElement element)
        {
            var assemblyName = element.FullyQualifiedName ?? element.Name;
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
            var namespaceName = element.FullyQualifiedName ?? element.Name;

            var namespaceNode = GetOrCreateNamespace(assemblyName, namespaceName, element.Name);
            MergeMetrics(namespaceNode.Node.Metrics, element.Metrics);
            MergeSource(namespaceNode.Node, element.Source);
        }

        private void MergeType(ParsedCodeElement element)
        {
            var typeFqn = element.FullyQualifiedName ?? element.Name;
            var assemblyName = ResolveAssemblyForType(element);
            var namespaceName = ResolveNamespaceName(typeFqn);

            var displayName = string.IsNullOrWhiteSpace(element.Name)
                ? ExtractTypeDisplayName(typeFqn)
                : element.Name.Contains('.') ? ExtractTypeDisplayName(typeFqn) : element.Name;

            var typeEntry = GetOrCreateType(assemblyName, namespaceName, typeFqn, displayName);
            MergeMetrics(typeEntry.Node.Metrics, element.Metrics);
            MergeSource(typeEntry.Node, element.Source);
        }

        private void MergeMember(ParsedCodeElement element)
        {
            if (element.FullyQualifiedName is null)
            {
                // Метрики SARIF агрегируются отдельно.
                return;
            }

            var memberFqn = element.FullyQualifiedName;
            var typeFqn = element.ParentFullyQualifiedName ?? ResolveDeclaringType(memberFqn);

            if (typeFqn is null)
            {
                return;
            }

            var typeEntry = EnsureTypeForMember(typeFqn);
            var memberNode = GetOrCreateMember(typeEntry, memberFqn, element.Name);

            MergeMetrics(memberNode.Metrics, element.Metrics);
            MergeSource(memberNode, element.Source);
        }

        private NamespaceEntry GetOrCreateNamespace(string assemblyName, string namespaceFqn, string displayName)
        {
            var key = $"{assemblyName}::{namespaceFqn}";
            if (_namespaces.TryGetValue(key, out var entry))
            {
                return entry;
            }

            if (string.IsNullOrEmpty(assemblyName))
            {
                assemblyName = ResolveAssemblyNameFromFqn(namespaceFqn);
            }

            var assembly = GetOrCreateAssembly(assemblyName, new ParsedCodeElement(CodeElementKind.Assembly, assemblyName, assemblyName));
            var node = new NamespaceMetricsNode
            {
                Name = displayName,
                FullyQualifiedName = namespaceFqn,
                Metrics = new Dictionary<MetricIdentifier, MetricValue>()
            };

            assembly.Namespaces.Add(node);
            entry = new NamespaceEntry(node, assembly);
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

            return assemblyNode;
        }

        private TypeEntry GetOrCreateType(string assemblyName, string namespaceName, string typeFqn, string displayName)
        {
            if (_types.TryGetValue(typeFqn, out var entry))
            {
                return entry;
            }

            var namespaceEntry = GetOrCreateNamespace(assemblyName, namespaceName, namespaceName);
            var node = new TypeMetricsNode
            {
                Name = displayName,
                FullyQualifiedName = typeFqn,
                Metrics = new Dictionary<MetricIdentifier, MetricValue>()
            };

            namespaceEntry.Node.Types.Add(node);
            entry = new TypeEntry(node, namespaceEntry.Assembly);
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
            if (_members.TryGetValue(memberFqn, out var node))
            {
                return node;
            }

            node = new MemberMetricsNode
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

        private AssemblyMetricsNode? TryResolveAssemblyForType(string typeFqn)
        {
            if (_types.TryGetValue(typeFqn, out var entry))
            {
                return entry.Assembly;
            }

            var namespaceName = ResolveNamespaceName(typeFqn);
            return TryResolveAssembly(namespaceName);
        }

        private AssemblyMetricsNode? TryResolveAssemblyForMember(string memberFqn)
        {
            var typeFqn = ResolveDeclaringType(memberFqn);
            return TryResolveAssemblyForType(typeFqn);
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

            return _assemblies.Keys.FirstOrDefault() ?? Solution.Name;
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

        private static string ExtractMemberDisplayName(string memberFqn, string fallback)
        {
            var lastDot = memberFqn.LastIndexOf('.');
            return lastDot < 0 ? fallback : memberFqn[(lastDot + 1)..];
        }

        private static string ResolveDeclaringType(string memberFqn)
        {
            var lastDot = memberFqn.LastIndexOf('.');
            return lastDot < 0 ? memberFqn : memberFqn[..lastDot];
        }

        private static string NormalizePath(string path)
            => path.Replace('/', '\\').Trim().ToUpperInvariant();

        private void RegisterFileAssembly(string normalizedPath, MetricsNode node)
        {
            switch (node)
            {
                case AssemblyMetricsNode assembly:
                    _fileAssemblyMap[normalizedPath] = assembly;
                    break;
                case TypeMetricsNode type when type.FullyQualifiedName is not null:
                    var assemblyForType = TryResolveAssemblyForType(type.FullyQualifiedName);
                    if (assemblyForType is not null)
                    {
                        _fileAssemblyMap[normalizedPath] = assemblyForType;
                    }

                    break;
                case MemberMetricsNode member when member.FullyQualifiedName is not null:
                    var assemblyForMember = TryResolveAssemblyForMember(member.FullyQualifiedName);
                    if (assemblyForMember is not null)
                    {
                        _fileAssemblyMap[normalizedPath] = assemblyForMember;
                    }

                    break;
            }
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

        private void ApplyBaselineRecursive(MetricsNode node, IReadOnlyDictionary<string, MetricsNode> baselineLookup, IDictionary<MetricIdentifier, MetricThreshold> thresholds, string path)
        {
            baselineLookup.TryGetValue(path, out var baselineNode);

            if (node != Solution)
            {
                node.IsNew = baselineNode is null;
            }

            node.Metrics = ApplyMetricsBaseline(node.Metrics, baselineNode?.Metrics ?? new Dictionary<MetricIdentifier, MetricValue>(), thresholds);

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
            IDictionary<MetricIdentifier, MetricThreshold> thresholds)
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

                var status = EvaluateStatus(identifier, value, thresholds);

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

        private static ThresholdStatus EvaluateStatus(MetricIdentifier identifier, decimal? value, IDictionary<MetricIdentifier, MetricThreshold> thresholds)
        {
            if (!value.HasValue)
            {
                return ThresholdStatus.NotApplicable;
            }

            if (!thresholds.TryGetValue(identifier, out var threshold))
            {
                return ThresholdStatus.NotApplicable;
            }

            var warning = threshold.Warning;
            var error = threshold.Error;

            if (threshold.HigherIsBetter)
            {
                if (error.HasValue && value < error)
                {
                    return ThresholdStatus.Error;
                }

                if (warning.HasValue && value < warning)
                {
                    return ThresholdStatus.Warning;
                }
            }
            else
            {
                if (error.HasValue && value > error)
                {
                    return ThresholdStatus.Error;
                }

                if (warning.HasValue && value > warning)
                {
                    return ThresholdStatus.Warning;
                }
            }

            return ThresholdStatus.Success;
        }

        private sealed record NamespaceEntry(NamespaceMetricsNode Node, AssemblyMetricsNode Assembly);

        private sealed record TypeEntry(TypeMetricsNode Node, AssemblyMetricsNode Assembly);

        private readonly record struct IndexedNode(MetricsNode Node, int StartLine, int EndLine);
    }
}

