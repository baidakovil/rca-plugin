#nullable enable
using System;

namespace Rca.Loader.Infrastructure
{
    /// <summary>
    /// Constants for assembly loading and runtime management.
    /// NOTE: Assemblies listed in NonCollectibleAssemblies are always loaded (or reused) from the default context.
    /// This avoids loading multiple copies across the collectible RuntimeLoadContext which would otherwise
    /// break type identity or leak memory.
    /// </summary>
    public static class AssemblyLoadConstants
    {
        /// <summary>
        /// Assembly names that must be loaded in the default context to avoid collectible assembly issues for DLR / IronPython.
        /// </summary>
        public static readonly string[] PythonAssemblies =
        {
            "IronPython",
            "IronPython.Modules",
            "IronPython.StdLib",
            "Microsoft.Scripting",
            "Microsoft.Dynamic",
            "DynamicLanguageRuntime"
        };

        /// <summary>
        /// Assembly names that should not be loaded into a collectible context. They are first resolved from Default context.
        /// Added Rca.Logging.Contracts to ensure a single shared copy is used for LogEntryDto across Loader + Runtime.
        /// </summary>
        public static readonly string[] NonCollectibleAssemblies =
        {
            "Rca.Loader.Contracts",
            "Rca.Logging.Contracts", // shared logging DTOs (avoid duplicate load + FileLoadException)
            "Rca.Contracts", // shared contracts used by Loader and Runtime
            "IronPython",
            "IronPython.Modules",
            "IronPython.StdLib",
            "Microsoft.Scripting",
            "Microsoft.Dynamic",
            "DynamicLanguageRuntime",
            "System.Numerics",
            "Microsoft.CSharp",
            "System.Dynamic.Runtime"
        };

        /// <summary>
        /// The standard DLL file extension.
        /// </summary>
        public const string DllExtension = ".dll";

        /// <summary>
        /// The contracts assembly name for special handling.
        /// </summary>
        public const string LoaderContractsAssembly = "Rca.Loader.Contracts";
    }
}
