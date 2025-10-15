#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Rca.Loader.Infrastructure
{
    /// <summary>
    /// Provides safe, disk-only access to <see cref="AssemblyMetadataAttribute"/> values on assemblies.
    /// </summary>
    /// <remarks>
    /// This helper never loads target assemblies into the Default load context. Instead it uses
    /// <see cref="System.Reflection.MetadataLoadContext"/> with a path-based resolver to read custom attributes
    /// directly from files on disk. If metadata cannot be read, the method returns a stable marker value
    /// <see cref="MissingMarker"/>. No alternative sources or heuristics are used.
    /// </remarks>
    public static class AttributeMetadataLoader
    {
        /// <summary>
        /// Marker returned when a metadata value is missing or cannot be read.
        /// </summary>
        public const string MissingMarker = "none";

        /// <summary>
        /// Attempts to read a metadata value from an already loaded assembly instance.
        /// </summary>
        /// <param name="asm">The loaded assembly.</param>
        /// <param name="key">The metadata key (for example, <see cref="BuildConstants.SourceHashMetadataKey"/>).</param>
        /// <returns>
        /// The metadata value if present; otherwise <see cref="MissingMarker"/>.
        /// </returns>
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
        /// Attempts to read a metadata value from an assembly file on disk without loading it into the Default context.
        /// Only attributes embedded in the DLL are considered.
        /// </summary>
        /// <param name="filePath">Full path to the assembly file.</param>
        /// <param name="key">The metadata key (for example, <see cref="BuildConstants.SourceHashMetadataKey"/>).</param>
        /// <returns>
        /// The metadata value if present; otherwise <see cref="MissingMarker"/>.
        /// </returns>
        public static string TryGetFromFile(string filePath, string key)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(key) || !File.Exists(filePath))
                    return MissingMarker;

                var (resolver, coreName) = CreatePathResolver(filePath);
                using var mlc = new System.Reflection.MetadataLoadContext(resolver, coreName);
                var asm = mlc.LoadFromAssemblyPath(Path.GetFullPath(filePath));
                if (asm == null)
                    return MissingMarker;

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
                    catch
                    {
                        // ignore malformed attribute entries and keep scanning
                    }
                }

                return MissingMarker;
            }
            catch
            {
                return MissingMarker;
            }
        }

        /// <summary>
        /// Creates a path-based resolver for <see cref="System.Reflection.MetadataLoadContext"/> seeded with
        /// the target assembly path, its directory, and known framework assemblies.
        /// </summary>
        /// <param name="primaryAssemblyPath">Full path to the assembly file to inspect.</param>
        /// <returns>A tuple containing the configured <see cref="PathAssemblyResolver"/> and the core assembly name.</returns>
        private static (PathAssemblyResolver Resolver, string CoreAssemblyName) CreatePathResolver(string primaryAssemblyPath)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Path.GetFullPath(primaryAssemblyPath)
            };

            var dir = Path.GetDirectoryName(primaryAssemblyPath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                foreach (var f in EnumerateDllsSafe(dir)) paths.Add(f);
            }

            var coreCandidates = new[]
            {
                typeof(object).Assembly,
                typeof(AssemblyMetadataAttribute).Assembly,
                typeof(Enumerable).Assembly,
                typeof(Uri).Assembly
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
                catch { }
            }

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

        /// <summary>
        /// Enumerates DLL files inside a directory defensively, skipping files that cannot be resolved to full paths.
        /// </summary>
        /// <param name="directory">The directory to scan.</param>
        /// <returns>An enumerable of full DLL paths in the specified directory.</returns>
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
