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
    /// Ensures dependent assemblies are loaded and registers minimal services.
    /// </summary>
    public class StandaloneRevitContext : IRevitContext
    {
        private readonly ILogger _log;

        /// <summary>
        /// Initializes a new instance of the StandaloneRevitContext class and ensures
        /// all required assemblies are loaded.
        /// </summary>
        public StandaloneRevitContext()
        {
            var provider = new NamedPipeLoggerProvider("RCA_LOG_PIPE", Guid.NewGuid().ToString("N"));
            _log = provider.CreateLogger(nameof(StandaloneRevitContext));
            EnsureAssembliesLoaded();
            _log.LogInformation("StandaloneRevitContext initialized");
        }

        /// <summary>
        /// Ensures all required assemblies for the standalone mode are loaded.
        /// </summary>
        private void EnsureAssembliesLoaded()
        {
            try
            {
                // Get the directory where the current runtime assembly is located
                string? runtimeDir = Path.GetDirectoryName(GetType().Assembly.Location);
                
                if (runtimeDir == null)
                {
                    _log.LogWarning("Runtime directory not determined");
                    return;
                }
                
                // List of assemblies we need to ensure are loaded
                string[] required = { "Rca.Core.dll", "Rca.UI.dll", "Rca.Network.dll", "Rca.Contracts.dll" };
                
                foreach (var asmFile in required)
                {
                    var path = Path.Combine(runtimeDir, asmFile);
                    
                    // Check if the assembly exists
                    if (!File.Exists(path)) { _log.LogDebug("Assembly file not found {File}", path); continue; }
                    try
                    {
                        // Try to load the assembly if it's not already loaded
                        if (!IsAssemblyLoaded(asmFile))
                        {
                            Assembly.LoadFrom(path);
                            _log.LogDebug("Loaded {Asm}", asmFile);
                        }
                        else
                        {
                            _log.LogTrace("Already loaded {Asm}", asmFile);
                        }
                    }
                    catch (Exception exLoad)
                    {
                        _log.LogWarning(exLoad, "Error loading assembly {Asm}", asmFile);
                    }
                }
                
                // Register any required services
                RegisterServices();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error ensuring assemblies loaded");
            }
        }
        
        /// <summary>
        /// Checks if an assembly with the given name is already loaded.
        /// </summary>
        /// <param name="assemblyFile">Name of the assembly file to check.</param>
        /// <returns>True if already loaded, false otherwise.</returns>
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
        /// Registers any additional services needed for standalone operation.
        /// </summary>
        private void RegisterServices()
        {
            try
            {
                var container = ServiceContainer.Instance;
                _log.LogDebug("ServiceContainer obtained for standalone context");
                
                // Future service registrations could be added here.
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Error registering services in standalone context");
            }
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
                return new object(); // Return a placeholder object instead of null
            }
            set
            {
                _log.LogTrace("Attempt to set CurrentUIApplication ignored in standalone context");
                // Intentionally ignored in standalone mode
            }
        }
    }
}
