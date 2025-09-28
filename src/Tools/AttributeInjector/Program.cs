using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;

namespace AttributeInjector
{
    class Program
    {
        // Usage:
        //  Inject mode: AttributeInjector <assemblyPath> <deployFolder> <sourceHash> [--out <outPath>] [additionalResolverDir]
        //  Inspect mode: AttributeInjector inspect <assemblyPath> [additionalResolverDir]
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Usage:\n  Inject: AttributeInjector <assemblyPath> <deployFolder> <sourceHash> [--out <outPath>] [additionalResolverDir]\n  Inspect: AttributeInjector inspect <assemblyPath> [additionalResolverDir]");
                return 2;
            }

            if (string.Equals(args[0], "inspect", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("Inspect usage: AttributeInjector inspect <assemblyPath> [additionalResolverDir]");
                    return 2;
                }

                var assemblyPath = args[1];
                var additionalResolverDir = args.Length >= 3 ? args[2] : null;
                return InspectAssembly(assemblyPath, additionalResolverDir);
            }

            // Inject mode
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Inject usage: AttributeInjector <assemblyPath> <deployFolder> <sourceHash> [--out <outPath>] [additionalResolverDir]");
                return 2;
            }

            var targetAssemblyPath = args[0];
            var deployFolder = args[1] ?? string.Empty;
            var sourceHash = args[2] ?? string.Empty;

            string? outPath = null;
            string? resolverDir = null;

            // parse optional args
            for (int i = 3; i < args.Length; i++)
            {
                if (args[i].Equals("--out", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    outPath = args[i + 1];
                    i++;
                    continue;
                }

                // remaining single positional argument is resolver dir
                if (resolverDir == null)
                    resolverDir = args[i];
            }

            if (!File.Exists(targetAssemblyPath))
            {
                Console.Error.WriteLine($"Assembly not found: {targetAssemblyPath}");
                return 3;
            }

            // Enforce that deployFolder is provided. Do not fallback to informational version or the hash.
            if (string.IsNullOrWhiteSpace(deployFolder))
            {
                Console.Error.WriteLine("AttributeInjector error: deployFolder is empty. DeployFolder must be provided and will be written as AssemblyMetadata('DeployFolder', ...) in the target assembly.");
                return 5;
            }

            return InjectAttributes(targetAssemblyPath, deployFolder, sourceHash, outPath, resolverDir);
        }

        private static string FullNameOrName(Type t) => t.FullName ?? t.Name;

        private static int InjectAttributes(string assemblyPath, string deployFolder, string sourceHash, string? outPath, string? additionalResolverDir)
        {
            try
            {
                var resolver = new FallbackAssemblyResolver();
                var asmDir = Path.GetDirectoryName(assemblyPath) ?? ".";
                resolver.AddSearchDirectory(asmDir);
                resolver.AddSearchDirectory(Directory.GetCurrentDirectory());

                if (!string.IsNullOrEmpty(additionalResolverDir) && Directory.Exists(additionalResolverDir))
                {
                    resolver.AddSearchDirectory(additionalResolverDir);
                }

                string? solutionRoot = FindSolutionRoot(asmDir);
                if (!string.IsNullOrEmpty(solutionRoot))
                {
                    var revitLib = Path.Combine(solutionRoot, "libs", "Revit", "2026");
                    if (Directory.Exists(revitLib)) resolver.AddSearchDirectory(revitLib);
                }

                var readParams = new ReaderParameters
                {
                    AssemblyResolver = resolver,
                    InMemory = true,
                    ReadingMode = ReadingMode.Deferred,
                    ReadWrite = true
                };

                using var asmDef = AssemblyDefinition.ReadAssembly(assemblyPath, readParams);
                if (asmDef == null)
                {
                    Console.Error.WriteLine($"Failed to read assembly: {assemblyPath}");
                    return 1;
                }

                var module = asmDef.MainModule;

                // Diagnostics: list existing AssemblyMetadata attributes
                var existingMd = asmDef.CustomAttributes
                    .Where(ca => string.Equals(ca.AttributeType.FullName, "System.Reflection.AssemblyMetadataAttribute", StringComparison.OrdinalIgnoreCase))
                    .Select(ca => ca.ConstructorArguments.Count >= 2 ? (ca.ConstructorArguments[0].Value as string ?? string.Empty) : string.Empty)
                    .ToList();
                Console.WriteLine($"Existing AssemblyMetadata keys before removal: count={existingMd.Count} [{string.Join(", ", existingMd)}]");

                // Import constructors for product-related attributes
                var prodCtor = module.ImportReference(typeof(System.Reflection.AssemblyProductAttribute).GetConstructor(new[] { typeof(string) }));
                var compCtor = module.ImportReference(typeof(System.Reflection.AssemblyCompanyAttribute).GetConstructor(new[] { typeof(string) }));
                var descCtor = module.ImportReference(typeof(System.Reflection.AssemblyDescriptionAttribute).GetConstructor(new[] { typeof(string) }));
                var titleCtor = module.ImportReference(typeof(System.Reflection.AssemblyTitleAttribute).GetConstructor(new[] { typeof(string) }));
                var fileVerCtor = module.ImportReference(typeof(System.Reflection.AssemblyFileVersionAttribute).GetConstructor(new[] { typeof(string) }));
                var infoVerCtor = module.ImportReference(typeof(System.Reflection.AssemblyInformationalVersionAttribute).GetConstructor(new[] { typeof(string) }));

                // Remove existing attributes of these types by AttributeType.FullName to avoid resolving constructor details
                var targetTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    FullNameOrName(typeof(System.Reflection.AssemblyProductAttribute)),
                    FullNameOrName(typeof(System.Reflection.AssemblyCompanyAttribute)),
                    FullNameOrName(typeof(System.Reflection.AssemblyDescriptionAttribute)),
                    FullNameOrName(typeof(System.Reflection.AssemblyTitleAttribute)),
                    FullNameOrName(typeof(System.Reflection.AssemblyFileVersionAttribute)),
                    FullNameOrName(typeof(System.Reflection.AssemblyInformationalVersionAttribute)),
                    FullNameOrName(typeof(System.Reflection.AssemblyMetadataAttribute))
                };

                var toRemove = asmDef.CustomAttributes
                    .Where(ca => targetTypeNames.Contains(ca.AttributeType.FullName))
                    .ToList();

                foreach (var ca in toRemove)
                {
                    asmDef.CustomAttributes.Remove(ca);
                }

                // Diagnostics: confirm removal
                var afterRemoveMd = asmDef.CustomAttributes
                    .Where(ca => string.Equals(ca.AttributeType.FullName, "System.Reflection.AssemblyMetadataAttribute", StringComparison.OrdinalIgnoreCase))
                    .Select(ca => ca.ConstructorArguments.Count >= 2 ? (ca.ConstructorArguments[0].Value as string ?? string.Empty) : string.Empty)
                    .ToList();
                Console.WriteLine($"AssemblyMetadata keys after removal: count={afterRemoveMd.Count} [{string.Join(", ", afterRemoveMd)}]");

                // Inject AssemblyMetadata SourceHash and DeployFolder (exactly two constructor args)
                var metaCtor = module.ImportReference(typeof(System.Reflection.AssemblyMetadataAttribute).GetConstructor(new[] { typeof(string), typeof(string) }));
                var deployAttr = new CustomAttribute(metaCtor);
                deployAttr.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, "DeployFolder"));
                deployAttr.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, deployFolder));
                asmDef.CustomAttributes.Add(deployAttr);

                var hashAttr = new CustomAttribute(metaCtor);
                hashAttr.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, "SourceHash"));
                hashAttr.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, sourceHash));
                asmDef.CustomAttributes.Add(hashAttr);

                // Diagnostics: list AssemblyMetadata keys after injection
                var afterInjectMd = asmDef.CustomAttributes
                    .Where(ca => string.Equals(ca.AttributeType.FullName, "System.Reflection.AssemblyMetadataAttribute", StringComparison.OrdinalIgnoreCase))
                    .Select(ca => ca.ConstructorArguments.Count >= 2 ? (ca.ConstructorArguments[0].Value as string ?? string.Empty) + "=" + (ca.ConstructorArguments[1].Value as string ?? string.Empty) : string.Empty)
                    .ToList();
                Console.WriteLine($"AssemblyMetadata keys after injection (in-memory): count={afterInjectMd.Count} [{string.Join(", ", afterInjectMd)}]");

                // Inject informational version
                var productVersion = $"DeployFolder: {deployFolder}, SourceHash: {sourceHash}";
                var infoAttr = new CustomAttribute(infoVerCtor);
                infoAttr.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, productVersion));
                asmDef.CustomAttributes.Add(infoAttr);

                // Inject product/company/description/title/fileversion for Explorer
                var product = $"RCA Runtime";
                var company = "RCA";
                var description = $"DeployFolder: {deployFolder}; SourceHash: {sourceHash}";
                var title = $"RCA Runtime (hash: {sourceHash})";
                var fileVersion = sourceHash; // or derive semver; using hash for uniqueness

                if (!string.IsNullOrEmpty(product))
                {
                    var a = new CustomAttribute(prodCtor);
                    a.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, product));
                    asmDef.CustomAttributes.Add(a);
                }
                if (!string.IsNullOrEmpty(company))
                {
                    var a = new CustomAttribute(compCtor);
                    a.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, company));
                    asmDef.CustomAttributes.Add(a);
                }
                if (!string.IsNullOrEmpty(description))
                {
                    var a = new CustomAttribute(descCtor);
                    a.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, description));
                    asmDef.CustomAttributes.Add(a);
                }
                if (!string.IsNullOrEmpty(title))
                {
                    var a = new CustomAttribute(titleCtor);
                    a.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, title));
                    asmDef.CustomAttributes.Add(a);
                }
                if (!string.IsNullOrEmpty(fileVersion))
                {
                    var a = new CustomAttribute(fileVerCtor);
                    a.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, fileVersion));
                    asmDef.CustomAttributes.Add(a);
                }

                // Determine output path: write to temp then move to outPath if provided, else replace original
                var finalPath = outPath ?? assemblyPath;
                var tempPath = finalPath + ".tmp";
                asmDef.Write(tempPath);

                // If writing to a different path and target file is locked, File.Copy will throw; let caller handle.
                File.Copy(tempPath, finalPath, true);
                File.Delete(tempPath);

                // Diagnostics: re-open written file and list metadata to confirm persistence
                try
                {
                    var readParams2 = new ReaderParameters { InMemory = true, ReadingMode = ReadingMode.Deferred, AssemblyResolver = resolver };
                    using var re = AssemblyDefinition.ReadAssembly(finalPath, readParams2);
                    var persisted = re.CustomAttributes
                        .Where(ca => string.Equals(ca.AttributeType.FullName, "System.Reflection.AssemblyMetadataAttribute", StringComparison.OrdinalIgnoreCase))
                        .Select(ca => ca.ConstructorArguments.Count >= 2 ? (ca.ConstructorArguments[0].Value as string ?? string.Empty) + "=" + (ca.ConstructorArguments[1].Value as string ?? string.Empty) : string.Empty)
                        .ToList();
                    Console.WriteLine($"AssemblyMetadata persisted on disk: count={persisted.Count} [{string.Join(", ", persisted)}]");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Inspect after write failed: {ex.Message}");
                }

                Console.WriteLine($"Injected attributes into {finalPath}");
                return 0;
            }
            catch (AssemblyResolutionException arex)
            {
                Console.Error.WriteLine($"Failed to inject attributes: assembly resolution failed: {arex.Message}");
                return 4;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to inject attributes: {ex}");
                return 1;
            }
        }

        private static int InspectAssembly(string assemblyPath, string? additionalResolverDir)
        {
            if (!File.Exists(assemblyPath))
            {
                Console.Error.WriteLine($"Assembly not found: {assemblyPath}");
                return 3;
            }

            try
            {
                var resolver = new FallbackAssemblyResolver();
                var asmDir = Path.GetDirectoryName(assemblyPath) ?? ".";
                resolver.AddSearchDirectory(asmDir);
                resolver.AddSearchDirectory(Directory.GetCurrentDirectory());
                if (!string.IsNullOrEmpty(additionalResolverDir) && Directory.Exists(additionalResolverDir))
                    resolver.AddSearchDirectory(additionalResolverDir);

                var readParams = new ReaderParameters
                {
                    AssemblyResolver = resolver,
                    InMemory = true,
                    ReadingMode = ReadingMode.Deferred
                };

                using var asmDef = AssemblyDefinition.ReadAssembly(assemblyPath, readParams);
                if (asmDef == null)
                {
                    Console.Error.WriteLine($"Failed to read assembly: {assemblyPath}");
                    return 1;
                }

                var attrs = asmDef.CustomAttributes;

                // Find AssemblyMetadata attributes
                var metadataAttrs = attrs.Where(ca => string.Equals(ca.AttributeType.FullName, "System.Reflection.AssemblyMetadataAttribute", StringComparison.OrdinalIgnoreCase)).ToList();
                if (metadataAttrs.Count == 0)
                {
                    Console.WriteLine("No AssemblyMetadata attributes found.");
                }
                else
                {
                    foreach (var ma in metadataAttrs)
                    {
                        string key = "";
                        string val = "";
                        try
                        {
                            if (ma.ConstructorArguments.Count >= 2)
                            {
                                key = ma.ConstructorArguments[0].Value as string ?? string.Empty;
                                val = ma.ConstructorArguments[1].Value as string ?? string.Empty;
                            }
                        }
                        catch
                        {
                            // ignore resolution issues
                        }

                        Console.WriteLine($"AssemblyMetadata: {key} = {val}");
                    }
                }

                // InformationalVersion
                var info = attrs.FirstOrDefault(ca => string.Equals(ca.AttributeType.FullName, "System.Reflection.AssemblyInformationalVersionAttribute", StringComparison.OrdinalIgnoreCase));
                if (info != null)
                {
                    try
                    {
                        var val = info.ConstructorArguments.Count > 0 ? info.ConstructorArguments[0].Value as string ?? string.Empty : string.Empty;
                        Console.WriteLine($"AssemblyInformationalVersion: {val}");
                    }
                    catch
                    {
                        Console.WriteLine("AssemblyInformationalVersion: <unresolved>");
                    }
                }

                // Also print basic file info
                Console.WriteLine($"File: {assemblyPath}");
                Console.WriteLine($"Module name: {asmDef.MainModule.Name}");

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Inspect failed: {ex}");
                return 1;
            }
        }

        private static string? FindSolutionRoot(string startDir)
        {
            try
            {
                var dir = new DirectoryInfo(startDir);
                while (dir != null)
                {
                    var sln = dir.GetFiles("*.sln").FirstOrDefault();
                    if (sln != null) return dir.FullName;
                    dir = dir.Parent;
                }
            }
            catch { }
            return null;
        }

        private class FallbackAssemblyResolver : DefaultAssemblyResolver
        {
            public override AssemblyDefinition Resolve(AssemblyNameReference name)
            {
                try { return base.Resolve(name); }
                catch (AssemblyResolutionException)
                {
                    var asmName = new AssemblyNameDefinition(name.Name, new Version(0, 0, 0, 0));
                    var module = ModuleDefinition.CreateModule(name.Name, ModuleKind.Dll);
                    var dummy = AssemblyDefinition.CreateAssembly(asmName, module.Name, ModuleKind.Dll);
                    return dummy;
                }
            }

            public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
            {
                try { return base.Resolve(name, parameters); }
                catch (AssemblyResolutionException)
                {
                    var asmName = new AssemblyNameDefinition(name.Name, new Version(0, 0, 0, 0));
                    var module = ModuleDefinition.CreateModule(name.Name, ModuleKind.Dll);
                    var dummy = AssemblyDefinition.CreateAssembly(asmName, module.Name, ModuleKind.Dll);
                    return dummy;
                }
            }
        }
    }
}
