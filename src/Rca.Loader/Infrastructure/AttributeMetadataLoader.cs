#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;

namespace Rca.Loader.Infrastructure
{
    /// <summary>
    /// Helper to read assembly metadata attributes from loaded assemblies (reflection)
    /// or from assembly files on disk (Mono.Cecil).
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
        /// Reads an AssemblyMetadata attribute from an assembly file on disk using Mono.Cecil.
        /// Returns <see cref="MissingMarker"/> when the key is not present or on error.
        /// This implementation uses deferred reading and a fallback resolver to avoid
        /// forcing resolution of unavailable referenced assemblies (for example RevitAPI).
        /// </summary>
        public static string TryGetFromFile(string filePath, string key)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(key) || !File.Exists(filePath))
                    return MissingMarker;

                // Setup a resolver that can search the file's directory and fall back if resolution fails.
                var resolver = new FallbackAssemblyResolver();
                var baseDir = Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();
                resolver.AddSearchDirectory(baseDir);

                var readParams = new ReaderParameters
                {
                    AssemblyResolver = resolver,
                    InMemory = true,
                    ReadingMode = ReadingMode.Deferred
                };

                using var asmDef = AssemblyDefinition.ReadAssembly(filePath, readParams);
                if (asmDef == null) return MissingMarker;

                var mdAttr = asmDef.CustomAttributes
                    .FirstOrDefault(ca =>
                        string.Equals(ca.AttributeType.FullName, "System.Reflection.AssemblyMetadataAttribute", StringComparison.OrdinalIgnoreCase)
                        && ca.ConstructorArguments.Count >= 2
                        && string.Equals((ca.ConstructorArguments[0].Value as string) ?? string.Empty, key, StringComparison.OrdinalIgnoreCase));

                if (mdAttr == null) return MissingMarker;

                var value = mdAttr.ConstructorArguments[1].Value as string;
                return string.IsNullOrEmpty(value) ? MissingMarker : value;
            }
            catch
            {
                return MissingMarker;
            }
        }

        // Fallback resolver that returns a minimal dummy AssemblyDefinition when resolution fails.
        // This prevents Mono.Cecil from throwing when referenced native SDK assemblies are not available
        // in the current environment (e.g. RevitAPI on build agents or developer machines without Revit SDK installed).
        private class FallbackAssemblyResolver : DefaultAssemblyResolver
        {
            public override AssemblyDefinition Resolve(AssemblyNameReference name)
            {
                try
                {
                    return base.Resolve(name);
                }
                catch (AssemblyResolutionException)
                {
                    var asmName = new AssemblyNameDefinition(name.Name, new Version(0, 0, 0, 0));
                    var module = ModuleDefinition.CreateModule(name.Name, ModuleKind.Dll);
                    return AssemblyDefinition.CreateAssembly(asmName, module.Name, ModuleKind.Dll);
                }
            }

            public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
            {
                try
                {
                    return base.Resolve(name, parameters);
                }
                catch (AssemblyResolutionException)
                {
                    var asmName = new AssemblyNameDefinition(name.Name, new Version(0, 0, 0, 0));
                    var module = ModuleDefinition.CreateModule(name.Name, ModuleKind.Dll);
                    return AssemblyDefinition.CreateAssembly(asmName, module.Name, ModuleKind.Dll);
                }
            }
        }
    }
}
