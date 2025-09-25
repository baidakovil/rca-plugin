using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Rca.Loader.Infrastructure
{
    /// <summary>
    /// Helper to read AssemblyMetadataAttribute values from a DLL file without loading it.
    /// Uses System.Reflection.Metadata to inspect assembly custom attributes.
    /// </summary>
    public static class AssemblyMetadataReader
    {
        /// <summary>
        /// Tries to read a metadata attribute value from the assembly file by key.
        /// Returns null if not found or on error.
        /// </summary>
        public static string? TryGetAssemblyMetadata(string assemblyPath, string key)
        {
            try
            {
                if (string.IsNullOrEmpty(assemblyPath) || !File.Exists(assemblyPath))
                    return null;

                using var stream = File.OpenRead(assemblyPath);
                using var peReader = new PEReader(stream, PEStreamOptions.LeaveOpen);
                if (!peReader.HasMetadata) return null;

                var mdReader = peReader.GetMetadataReader();
                var assemblyDef = mdReader.GetAssemblyDefinition();

                foreach (var handle in assemblyDef.GetCustomAttributes())
                {
                    var attribute = mdReader.GetCustomAttribute(handle);
                    var ctor = attribute.Constructor;

                    string? attributeTypeName = null;

                    if (ctor.Kind == HandleKind.MemberReference)
                    {
                        var memberRef = mdReader.GetMemberReference((MemberReferenceHandle)ctor);
                        var container = memberRef.Parent;
                        if (container.Kind == HandleKind.TypeReference)
                        {
                            var typeRef = mdReader.GetTypeReference((TypeReferenceHandle)container);
                            var name = mdReader.GetString(typeRef.Name);
                            var ns = mdReader.GetString(typeRef.Namespace);
                            attributeTypeName = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
                        }
                    }
                    else if (ctor.Kind == HandleKind.MethodDefinition)
                    {
                        var methodDef = mdReader.GetMethodDefinition((MethodDefinitionHandle)ctor);
                        var typeDef = mdReader.GetTypeDefinition(methodDef.GetDeclaringType());
                        var name = mdReader.GetString(typeDef.Name);
                        var ns = mdReader.GetString(typeDef.Namespace);
                        attributeTypeName = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
                    }

                    if (string.IsNullOrEmpty(attributeTypeName)) continue;

                    if (!attributeTypeName.EndsWith("AssemblyMetadataAttribute", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Read raw blob and attempt to extract two consecutive UTF8 strings
                    var blob = mdReader.GetBlobBytes(attribute.Value);
                    if (blob == null || blob.Length == 0) continue;

                    var s = Encoding.UTF8.GetString(blob);
                    var parts = s.Split('\0', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var foundKey = parts[0];
                        var foundVal = parts[1];
                        if (string.Equals(foundKey, key, StringComparison.OrdinalIgnoreCase))
                            return foundVal;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
