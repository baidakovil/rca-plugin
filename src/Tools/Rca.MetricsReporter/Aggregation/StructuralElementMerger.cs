namespace Rca.Tools.MetricsReporter.Aggregation;

using System;
using System.Collections.Generic;
using System.Linq;
using Rca.Tools.MetricsReporter.Model;
using Rca.Tools.MetricsReporter.Processing;

/// <summary>
/// Encapsulates all structural merging logic for assemblies, namespaces, types, and members.
/// </summary>
/// <remarks>
/// This helper keeps the workspace focused on orchestration while maintaining the original
/// filtering and merging behavior from <see cref="MetricsAggregationService.AggregationWorkspace"/>.
/// </remarks>
internal sealed class StructuralElementMerger
{
  private readonly SolutionMetricsNode _solution;
  private readonly Dictionary<string, AssemblyMetricsNode> _assemblies;
  private readonly Dictionary<string, NamespaceEntry> _namespaces;
  private readonly Dictionary<string, List<NamespaceEntry>> _namespaceIndex;
  private readonly Dictionary<string, TypeEntry> _types;
  private readonly Dictionary<string, MemberMetricsNode> _members;
  private readonly MemberFilter _memberFilter;
  private readonly AssemblyFilter _assemblyFilter;
  private readonly TypeFilter _typeFilter;

  /// <summary>
  /// Initializes a new instance of the <see cref="StructuralElementMerger"/> class.
  /// </summary>
  public StructuralElementMerger(
      SolutionMetricsNode solution,
      Dictionary<string, AssemblyMetricsNode> assemblies,
      Dictionary<string, NamespaceEntry> namespaces,
      Dictionary<string, List<NamespaceEntry>> namespaceIndex,
      Dictionary<string, TypeEntry> types,
      Dictionary<string, MemberMetricsNode> members,
      MemberFilter memberFilter,
      AssemblyFilter assemblyFilter,
      TypeFilter typeFilter)
  {
    _solution = solution ?? throw new ArgumentNullException(nameof(solution));
    _assemblies = assemblies ?? throw new ArgumentNullException(nameof(assemblies));
    _namespaces = namespaces ?? throw new ArgumentNullException(nameof(namespaces));
    _namespaceIndex = namespaceIndex ?? throw new ArgumentNullException(nameof(namespaceIndex));
    _types = types ?? throw new ArgumentNullException(nameof(types));
    _members = members ?? throw new ArgumentNullException(nameof(members));
    _memberFilter = memberFilter ?? throw new ArgumentNullException(nameof(memberFilter));
    _assemblyFilter = assemblyFilter ?? throw new ArgumentNullException(nameof(assemblyFilter));
    _typeFilter = typeFilter ?? throw new ArgumentNullException(nameof(typeFilter));
  }

  /// <summary>
  /// Merges an assembly element into the shared metrics trees.
  /// </summary>
  /// <summary>
  /// Merges metrics for an assembly element into the shared solution tree.
  /// </summary>
  /// <param name="element">The parsed assembly element to merge.</param>
  public void MergeAssembly(ParsedCodeElement element)
  {
    var assemblyName = element.FullyQualifiedName ?? element.Name;

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
      _solution.Assemblies.Add(assemblyNode);
    }

    MergeMetrics(assemblyNode.Metrics, element.Metrics);
    MergeSource(assemblyNode, element.Source);
  }

  /// <summary>
  /// Merges a namespace element under the appropriate assembly.
  /// </summary>
  /// <summary>
  /// Registers or updates a namespace node beneath its assembly.
  /// </summary>
  /// <param name="element">The parsed namespace element to merge.</param>
  public void MergeNamespace(ParsedCodeElement element)
  {
    var assemblyName = element.ParentFullyQualifiedName ?? string.Empty;

    if (_assemblyFilter.ShouldExcludeAssembly(assemblyName))
    {
      return;
    }

    var namespaceName = element.FullyQualifiedName ?? element.Name;
    var namespaceNode = GetOrCreateNamespace(assemblyName, namespaceName, element.Name);

    if (!_assemblies.ContainsKey(assemblyName))
    {
      return;
    }

    MergeMetrics(namespaceNode.Node.Metrics, element.Metrics);
    MergeSource(namespaceNode.Node, element.Source);
  }

  /// <summary>
  /// Merges a type element while respecting filters.
  /// </summary>
  /// <summary>
  /// Adds or updates a type node, respecting the configured filters.
  /// </summary>
  /// <param name="element">The parsed type element to merge.</param>
  public void MergeType(ParsedCodeElement element)
  {
    var typeFqn = element.FullyQualifiedName ?? element.Name;
    var assemblyName = ResolveAssemblyForType(element);

    if (_typeFilter.ShouldExcludeType(typeFqn) || _typeFilter.ShouldExcludeType(element.Name))
    {
      return;
    }

    if (_assemblyFilter.ShouldExcludeAssembly(assemblyName))
    {
      return;
    }

    var namespaceName = ResolveNamespaceName(typeFqn);
    var displayName = string.IsNullOrWhiteSpace(element.Name)
        ? ExtractTypeDisplayName(typeFqn)
        : element.Name.Contains('.') ? ExtractTypeDisplayName(typeFqn) : element.Name;

    var typeEntry = GetOrCreateType(assemblyName, namespaceName, typeFqn, displayName);

    if (!_assemblies.ContainsKey(assemblyName))
    {
      return;
    }

    MergeMetrics(typeEntry.Node.Metrics, element.Metrics);
    MergeSource(typeEntry.Node, element.Source);
  }

  /// <summary>
  /// Merges a member element, ensuring filters and assemblies are honored.
  /// </summary>
  /// <summary>
  /// Adds or updates a member (method/property) within its declaring type.
  /// </summary>
  /// <param name="element">The parsed member element to merge.</param>
  public void MergeMember(ParsedCodeElement element)
  {
    if (element.FullyQualifiedName is null)
    {
      return;
    }

    var memberFqn = element.FullyQualifiedName;

    if (_memberFilter.ShouldExcludeMethodByFqn(memberFqn))
    {
      return;
    }

    var typeFqn = element.ParentFullyQualifiedName ?? ResolveDeclaringType(memberFqn);
    if (typeFqn is null || _typeFilter.ShouldExcludeType(typeFqn))
    {
      return;
    }

    var assemblyName = ResolveAssemblyNameFromFqn(typeFqn);
    if (_assemblyFilter.ShouldExcludeAssembly(assemblyName))
    {
      return;
    }

    var typeEntry = EnsureTypeForMember(typeFqn);
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
    if (string.IsNullOrEmpty(assemblyName))
    {
      assemblyName = ResolveAssemblyNameFromFqn(namespaceFqn);
    }

    if (_assemblyFilter.ShouldExcludeAssembly(assemblyName))
    {
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
      var existingAssemblyName = existingEntry.Assembly.FullyQualifiedName;
      if (existingAssemblyName is not null && (_assemblyFilter.ShouldExcludeAssembly(existingAssemblyName) || !_assemblies.ContainsKey(existingAssemblyName)))
      {
        _namespaces.Remove(key);
        if (_namespaceIndex.TryGetValue(namespaceFqn, out var indexList))
        {
          indexList.Remove(existingEntry);
        }

        if (_assemblies.TryGetValue(existingAssemblyName, out var existingAssembly) && existingAssembly.Namespaces.Contains(existingEntry.Node))
        {
          existingAssembly.Namespaces.Remove(existingEntry.Node);
        }

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
    if (!_assemblies.ContainsKey(assemblyName))
    {
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
      list = [];
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
    if (_assemblyFilter.ShouldExcludeAssembly(assemblyName))
    {
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
      _solution.Assemblies.Add(assemblyNode);
    }

    MergeMetrics(assemblyNode.Metrics, element.Metrics);
    MergeSource(assemblyNode, element.Source);
    return assemblyNode;
  }

  private TypeEntry GetOrCreateType(string assemblyName, string namespaceName, string typeFqn, string displayName)
  {
    if (_types.TryGetValue(typeFqn, out var existingEntry))
    {
      if (assemblyName is not null && (_assemblyFilter.ShouldExcludeAssembly(assemblyName) || !_assemblies.ContainsKey(assemblyName)))
      {
        _types.Remove(typeFqn);
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

    if (_assemblyFilter.ShouldExcludeAssembly(assemblyName))
    {
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
    if (!_assemblies.ContainsKey(assemblyName))
    {
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
    if (_members.TryGetValue(memberFqn, out var existingNode))
    {
      var assemblyName = typeEntry.Assembly.FullyQualifiedName;
      if (assemblyName is not null && (_assemblyFilter.ShouldExcludeAssembly(assemblyName) || !_assemblies.ContainsKey(assemblyName)))
      {
        _members.Remove(memberFqn);
        return new MemberMetricsNode
        {
          Name = ExtractMemberDisplayName(memberFqn, displayName),
          FullyQualifiedName = memberFqn,
          Metrics = new Dictionary<MetricIdentifier, MetricValue>()
        };
      }

      return existingNode;
    }

    var assemblyName2 = typeEntry.Assembly.FullyQualifiedName;
    if (assemblyName2 is not null && (_assemblyFilter.ShouldExcludeAssembly(assemblyName2) || !_assemblies.ContainsKey(assemblyName2)))
    {
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

  private static void MergeMetrics(IDictionary<MetricIdentifier, MetricValue> target, IDictionary<MetricIdentifier, MetricValue> source)
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

  private static void MergeSource(MetricsNode node, SourceLocation? source)
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

  private AssemblyMetricsNode? TryResolveAssembly(string namespaceFqn)
  {
    if (!_namespaceIndex.TryGetValue(namespaceFqn, out var entries) || entries.Count == 0)
    {
      return null;
    }

    return entries[0].Assembly;
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

    return _assemblies.Keys.Count > 0 ? _assemblies.Keys.First() : _solution.Name;
  }

  private static string ResolveNamespaceName(string typeFqn)
  {
    var lastDot = typeFqn.LastIndexOf('.');
    return lastDot <= 0 ? "<global>" : typeFqn[..lastDot];
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

  private static string ExtractTypeDisplayName(string typeFqn)
  {
    var lastDot = typeFqn.LastIndexOf('.');
    return lastDot < 0 ? typeFqn : typeFqn[(lastDot + 1)..];
  }

  private static string ExtractMemberDisplayName(string memberFqn, string fallback)
  {
    if (string.IsNullOrWhiteSpace(memberFqn))
    {
      return fallback;
    }

    var paramStart = memberFqn.IndexOf('(');
    var searchEnd = paramStart >= 0 ? paramStart : memberFqn.Length;
    var lastDot = memberFqn.LastIndexOf('.', searchEnd - 1);

    if (lastDot < 0)
    {
      if (paramStart >= 0)
      {
        return memberFqn[..paramStart];
      }

      return fallback;
    }

    var methodNameStart = lastDot + 1;
    var methodNameEnd = paramStart >= 0 ? paramStart : memberFqn.Length;
    var methodName = memberFqn[methodNameStart..methodNameEnd].Trim();

    return string.IsNullOrWhiteSpace(methodName) ? fallback : methodName;
  }

  private static string ResolveDeclaringType(string memberFqn)
  {
    if (string.IsNullOrWhiteSpace(memberFqn))
    {
      return memberFqn;
    }

    var paramStart = memberFqn.IndexOf('(');
    var searchEnd = paramStart >= 0 ? paramStart : memberFqn.Length;
    var lastDot = memberFqn.LastIndexOf('.', searchEnd - 1);
    return lastDot < 0 ? memberFqn : memberFqn[..lastDot];
  }

  private static bool IsAggregatableMetric(MetricIdentifier identifier)
      => identifier is MetricIdentifier.SarifCaRuleViolations or MetricIdentifier.SarifIdeRuleViolations;
}

