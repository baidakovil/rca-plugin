namespace Rca.Tools.MetricsReporter.Aggregation;

using System;
using System.Collections.Generic;
using Rca.Tools.MetricsReporter.Model;

internal sealed class MetricsNodeLookup
{
  private readonly Dictionary<string, MetricsNode> _index;

  private MetricsNodeLookup(Dictionary<string, MetricsNode> index)
  {
    _index = index ?? throw new ArgumentNullException(nameof(index));
  }

  public static MetricsNodeLookup Create(SolutionMetricsNode solution)
  {
    if (solution is null)
    {
      throw new ArgumentNullException(nameof(solution));
    }

    var index = new Dictionary<string, MetricsNode>(StringComparer.Ordinal);

    void AddNode(MetricsNode? node)
    {
      if (node is null || string.IsNullOrWhiteSpace(node.FullyQualifiedName))
      {
        return;
      }

      index[node.FullyQualifiedName] = node;
    }

    foreach (var assembly in solution.Assemblies)
    {
      AddNode(assembly);
      foreach (var ns in assembly.Namespaces)
      {
        AddNode(ns);
        foreach (var type in ns.Types)
        {
          AddNode(type);
          foreach (var member in type.Members)
          {
            AddNode(member);
          }
        }
      }
    }

    return new MetricsNodeLookup(index);
  }

  public bool TryGetNode(string fullyQualifiedName, out MetricsNode node)
  {
    if (string.IsNullOrWhiteSpace(fullyQualifiedName))
    {
      node = null!;
      return false;
    }

    return _index.TryGetValue(fullyQualifiedName, out node);
  }
}

