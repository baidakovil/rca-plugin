#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Rca.Loader.Infrastructure
{
    /// <summary>
    /// Helper to read assembly metadata attributes from loaded assemblies (reflection)
    /// or from assembly files on disk without loading them into the default context.
    /// Always returns <see cref="MissingMarker"/> when the requested metadata cannot be read.
    /// </summary>
    public static class AttributeMetadataLoader
    {
        /// <summary>
        /// Marker returned when metadata cannot be read. Use a value without path separators.
        /// </summary>
        public const string MissingMarker = "none";

        /// <summary>
        /// Reads an AssemblyMetadata attribute from a loaded assembly using reflection.
        /// Returns <see cref="MissingMarker"/> when the key is not present or on error.
        /// NOTE: Prefer <see cref="TryGetFromFile"/> to follow the rule of reading metadata from disk.
        /// </summary>
        public static string TryGetFromLoadedAssembly(Assembly asm, string key)
        {
            try
            {
                if (asm == null || string.IsNullOrWhiteSpace(key))
                    return MissingMarker;

                var attrs = asm.GetCustomAttributes<AssemblyMetadataAttribute>();
                var match = attrs.FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase));
                return string.IsNullOrEmpty(match?.Value) ? MissingMarker : match.Value;
            }
            catch
            {
                return MissingMarker;
            }
        }

        /// <summary>
        /// Reads an AssemblyMetadata attribute from an assembly file on disk using MetadataLoadContext.
        /// Returns <see cref="MissingMarker"/> when the key is not present or on error.
        /// This does not load the assembly into the default context and avoids executing code.
        /// </summary>
        public static string TryGetFromFile(string filePath, string key)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(key) || !File.Exists(filePath))
                    return MissingMarker;

                var (resolver, coreName) = CreatePathResolver(filePath);
                using var mlc = new System.Reflection.MetadataLoadContext(resolver, coreName);
                var asm = mlc.LoadFromAssemblyPath(Path.GetFullPath(filePath));
                if (asm != null)
                {
                    foreach (var cad in asm.GetCustomAttributesData())
                    {
                        try
                        {
                            if (!string.Equals(cad.AttributeType.FullName, typeof(AssemblyMetadataAttribute).FullName, StringComparison.Ordinal))
                                continue;
                            if (cad.ConstructorArguments.Count < 2)
                                continue;
                            var k = cad.ConstructorArguments[0].Value as string;
                            if (!string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                                continue;
                            var v = cad.ConstructorArguments[1].Value as string;
                            return string.IsNullOrEmpty(v) ? MissingMarker : v;
                        }
                        catch { }
                    }
                }
            }
            catch
            {
                // ignore and try fallback
            }

            // Fallback path: version marker files in the same directory (written by build targets)
            try
            {
                if (string.Equals(key, BuildConstants.SourceHashMetadataKey, StringComparison.Ordinal))
                {
                    var dir = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        var rt = Directory.GetFiles(dir, "SourceHash-Runtime-*.txt").FirstOrDefault();
                        if (!string.IsNullOrEmpty(rt))
                        {
                            var line = SafeReadFirstLine(rt);
                            if (!string.IsNullOrWhiteSpace(line)) return line.Trim();
                        }
                        var ld = Directory.GetFiles(dir, "SourceHash-Loader-*.txt").FirstOrDefault();
                        if (!string.IsNullOrEmpty(ld))
                        {
                            var line = SafeReadFirstLine(ld);
                            if (!string.IsNullOrWhiteSpace(line)) return line.Trim();
                        }
                    }
                }
            }
            catch { }

            return MissingMarker;
        }

        private static string SafeReadFirstLine(string path)
        {
            try
            {
                using var sr = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                return sr.ReadLine() ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        private static (PathAssemblyResolver Resolver, string CoreAssemblyName) CreatePathResolver(string primaryAssemblyPath)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Path.GetFullPath(primaryAssemblyPath)
            };

            // Include directory of the target assembly
            var dir = Path.GetDirectoryName(primaryAssemblyPath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                foreach (var f in EnumerateDllsSafe(dir)) paths.Add(f);
            }

            // Add essential core assemblies explicitly
            var coreCandidates = new[]
            {
                typeof(object).Assembly,                                  // System.Private.CoreLib
                typeof(AssemblyMetadataAttribute).Assembly,               // System.Runtime / System.Reflection
                typeof(Enumerable).Assembly,                              // System.Linq
                typeof(Uri).Assembly                                      // System.Private.Uri / System.Runtime.Extensions
            };

            string coreAssemblyName = typeof(object).Assembly.GetName().Name ?? "System.Private.CoreLib";

            foreach (var asm in coreCandidates)
            {
                try
                {
                    var loc = asm.Location;
                    if (!string.IsNullOrEmpty(loc) && File.Exists(loc))
                    {
                        var full = Path.GetFullPath(loc);
                        paths.Add(full);
                        var coreDir = Path.GetDirectoryName(full);
                        if (!string.IsNullOrEmpty(coreDir) && Directory.Exists(coreDir))
                        {
                            foreach (var f in EnumerateDllsSafe(coreDir)) paths.Add(f);
                        }
                    }
                }
                catch { /* ignore */ }
            }

            // Also try to add already loaded framework assemblies with valid locations
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.IsDynamic) continue;
                    var loc = asm.Location;
                    if (string.IsNullOrEmpty(loc)) continue;
                    var full = Path.GetFullPath(loc);
                    paths.Add(full);
                }
                catch { }
            }

            return (new PathAssemblyResolver(paths), coreAssemblyName);
        }

        private static IEnumerable<string> EnumerateDllsSafe(string directory)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory, "*.dll");
            }
            catch
            {
                yield break;
            }

            foreach (var f in files)
            {
                string full;
                try { full = Path.GetFullPath(f); }
                catch { continue; }
                yield return full;
            }
        }
    }
}
