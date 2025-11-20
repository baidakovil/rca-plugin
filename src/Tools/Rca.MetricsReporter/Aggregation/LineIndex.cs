namespace Rca.Tools.MetricsReporter.Aggregation;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Rca.Tools.MetricsReporter.Model;

/// <summary>
/// Builds and queries line-based indexes for members and types.
/// </summary>
internal sealed class LineIndex
{
  private readonly Dictionary<string, List<IndexedNode>> _memberLineIndex = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, List<IndexedNode>> _typeLineIndex = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, AssemblyMetricsNode> _fileAssemblyMap = new(StringComparer.OrdinalIgnoreCase);

  /// <summary>
  /// Adds a member to the member index.
  /// </summary>
  public void AddMember(string normalizedPath, MemberMetricsNode member, int startLine, int endLine)
  {
    var list = GetOrCreateIndexList(_memberLineIndex, normalizedPath);
    list.Add(new IndexedNode(member, startLine, endLine));
  }

  /// <summary>
  /// Adds a type to the type index.
  /// </summary>
  public void AddType(string normalizedPath, TypeMetricsNode type, int startLine, int endLine)
  {
    var list = GetOrCreateIndexList(_typeLineIndex, normalizedPath);
    list.Add(new IndexedNode(type, startLine, endLine));
  }

  /// <summary>
  /// Registers the assembly that owns a file path.
  /// </summary>
  public void RegisterFileAssembly(string normalizedPath, AssemblyMetricsNode assembly)
  {
    if (assembly is null)
    {
      throw new ArgumentNullException(nameof(assembly));
    }

    _fileAssemblyMap[normalizedPath] = assembly;
  }

  /// <summary>
  /// Sorts all indexes for faster binary search.
  /// </summary>
  public void SortIndexes()
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
  /// Finds a metrics node that contains the specified line.
  /// </summary>
  public MetricsNode? FindNode(string normalizedPath, int line)
  {
    var node = FindNodeInIndex(_memberLineIndex, normalizedPath, line);
    return node ?? FindNodeInIndex(_typeLineIndex, normalizedPath, line);
  }

  /// <summary>
  /// Tries to lookup the assembly associated with the specified file.
  /// </summary>
  public bool TryGetAssembly(string normalizedPath, [MaybeNullWhen(false)] out AssemblyMetricsNode assembly)
      => _fileAssemblyMap.TryGetValue(normalizedPath, out assembly);

  private static List<IndexedNode> GetOrCreateIndexList(
      Dictionary<string, List<IndexedNode>> index,
      string path)
  {
    if (!index.TryGetValue(path, out var list))
    {
      list = [];
      index[path] = list;
    }

    return list;
  }

  private static MetricsNode? FindNodeInIndex(
      Dictionary<string, List<IndexedNode>> index,
      string path,
      int line)
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

  private readonly record struct IndexedNode(MetricsNode Node, int StartLine, int EndLine);
}

