using System;
using System.Reflection;
using System.IO;
using Rca.Contracts;
using Rca.Contracts.Infrastructure;
using Microsoft.Extensions.Logging;
using Rca.Runtime.Logging;

namespace Rca.Runtime
{
    /// <summary>
    /// Standalone implementation of IRevitContext used when executing outside Revit.
    /// Ensures only shared contracts are preloaded into the Default ALC to avoid type identity issues,
    /// and keeps UI/runtime assemblies loadable into the collectible RuntimeLoadContext for hot reload.
    /// </summary>
    public class StandaloneRevitContext : IRevitContext
    {
        private readonly ILogger _log;

        /// <summary>
        /// Initializes a new instance of the StandaloneRevitContext class and ensures
        /// required shared assemblies are loaded.
        /// </summary>
        public StandaloneRevitContext()
        {
            var provider = new NamedPipeLoggerProvider("RCA_LOG_PIPE", Guid.NewGuid().ToString("N"));
            _log = provider.CreateLogger(nameof(StandaloneRevitContext));
            EnsureSharedContractsLoaded();
            _log.LogInformation("StandaloneRevitContext initialized");
        }

        /// <summary>
        /// Preloads only shared contracts in Default ALC.
        /// Do not preload Rca.UI, Rca.Core, Rca.Network, etc., so they can be reloaded inside the collectible context.
        /// </summary>
        private void EnsureSharedContractsLoaded()
        {
            try
            {
                string? runtimeDir = Path.GetDirectoryName(GetType().Assembly.Location);
                if (runtimeDir == null)
                {
                    _log.LogWarning("Runtime directory not determined");
                    return;
                }

                string[] required = { "Rca.Contracts.dll" };

                foreach (var asmFile in required)
                {
                    var path = Path.Combine(runtimeDir, asmFile);
                    if (!File.Exists(path)) { _log.LogDebug("Shared contract file not found {File}", path); continue; }
                    try
                    {
                        if (!IsAssemblyLoaded(asmFile))
                        {
                            Assembly.LoadFrom(path);
                            _log.LogDebug("Preloaded shared contract {Asm} into Default ALC", asmFile);
                        }
                        else
                        {
                            _log.LogTrace("Shared contract already loaded {Asm}", asmFile);
                        }
                    }
                    catch (Exception exLoad)
                    {
                        _log.LogWarning(exLoad, "Error preloading shared contract {Asm}", asmFile);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error ensuring shared contracts loaded");
            }
        }

        /// <summary>
        /// Checks if an assembly with the given file name is already loaded in the current AppDomain.
        /// </summary>
        private bool IsAssemblyLoaded(string assemblyFile)
        {
            string name = Path.GetFileNameWithoutExtension(assemblyFile);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.IsDynamic && string.Equals(asm.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Gets or sets the current UI application.
        /// In standalone mode, this will always return a null-like placeholder object.
        /// </summary>
        public object CurrentUIApplication
        {
            get
            {
                _log.LogTrace("Access CurrentUIApplication placeholder");
                return new object();
            }
            set
            {
                _log.LogTrace("Attempt to set CurrentUIApplication ignored in standalone context");
            }
        }
    }
}
