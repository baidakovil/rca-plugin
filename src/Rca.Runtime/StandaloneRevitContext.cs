using System;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using Rca.Contracts;
using Rca.Contracts.Infrastructure;

namespace Rca.Runtime
{
    /// <summary>
    /// A standalone implementation of IRevitContext for use when running outside of Revit.
    /// Also ensures required assemblies are loaded correctly.
    /// </summary>
    public class StandaloneRevitContext : IRevitContext
    {
        /// <summary>
        /// Initializes a new instance of the StandaloneRevitContext class and ensures
        /// all required assemblies are loaded.
        /// </summary>
        public StandaloneRevitContext()
        {
            // Ensure all required assemblies are loaded
            EnsureAssembliesLoaded();
            
            Debug.WriteLine("StandaloneRevitContext initialized successfully");
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
                    Debug.WriteLine("Could not determine runtime directory");
                    return;
                }
                
                // List of assemblies we need to ensure are loaded
                string[] requiredAssemblies = new[]
                {
                    "Rca.Core.dll",
                    "Rca.UI.dll",
                    "Rca.Network.dll",
                    "Rca.Contracts.dll"
                };
                
                foreach (var assemblyName in requiredAssemblies)
                {
                    string assemblyPath = Path.Combine(runtimeDir, assemblyName);
                    
                    // Check if the assembly exists
                    if (File.Exists(assemblyPath))
                    {
                        try
                        {
                            // Try to load the assembly if it's not already loaded
                            if (!IsAssemblyLoaded(assemblyName))
                            {
                                Debug.WriteLine($"Loading assembly: {assemblyName}");
                                Assembly.LoadFrom(assemblyPath);
                                Debug.WriteLine($"Successfully loaded: {assemblyName}");
                            }
                            else
                            {
                                Debug.WriteLine($"Assembly already loaded: {assemblyName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error loading assembly {assemblyName}: {ex.Message}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Assembly file not found: {assemblyPath}");
                    }
                }
                
                // Register any required services
                RegisterServices();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error ensuring assemblies loaded: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Checks if an assembly with the given name is already loaded.
        /// </summary>
        /// <param name="assemblyName">Name of the assembly file to check.</param>
        /// <returns>True if already loaded, false otherwise.</returns>
        private bool IsAssemblyLoaded(string assemblyName)
        {
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(assemblyName);
            
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.IsDynamic && 
                    string.Equals(asm.GetName().Name, nameWithoutExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
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
                Debug.WriteLine("Obtained ServiceContainer instance");
                
                // Any additional service registrations can go here
                // Example: if (!container.IsRegistered<ISomeService>())
                //          container.Register<ISomeService>(new SomeServiceImpl());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error registering services: {ex.Message}");
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
                Debug.WriteLine("StandaloneRevitContext: Accessing CurrentUIApplication (null-placeholder in standalone mode)");
                return new object(); // Return a placeholder object instead of null
            }
            set
            {
                Debug.WriteLine("StandaloneRevitContext: Setting CurrentUIApplication (ignored in standalone mode)");
                // Intentionally ignored in standalone mode
            }
        }
    }
}