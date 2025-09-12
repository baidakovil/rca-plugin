#nullable enable
using System;

namespace Rca.Loader.Infrastructure
{
    /// <summary>
    /// Constants for assembly loading and runtime management.
    /// </summary>
    public static class AssemblyLoadConstants
    {
        /// <summary>
        /// Assembly names that must be loaded in the default context to avoid collectible assembly issues.
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
        /// Assembly names that should not be loaded in collectible contexts.
        /// </summary>
        public static readonly string[] NonCollectibleAssemblies = 
        {
            "Rca.Loader.Contracts", 
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