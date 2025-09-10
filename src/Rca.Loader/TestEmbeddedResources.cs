using System;
using System.Reflection;

namespace Rca.Loader
{
    /// <summary>
    /// Simple test class to verify embedded resources are accessible.
    /// This can be removed after confirming icons work.
    /// </summary>
    public static class TestEmbeddedResources
    {
        public static void ListAllResources()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var resources = asm.GetManifestResourceNames();
                
                Console.WriteLine($"Assembly: {asm.GetName().Name}");
                Console.WriteLine($"Location: {asm.Location}");
                Console.WriteLine($"Total embedded resources: {resources.Length}");
                
                foreach (var resource in resources)
                {
                    Console.WriteLine($"  - {resource}");
                    
                    // Test if we can actually open the resource
                    try
                    {
                        using var stream = asm.GetManifestResourceStream(resource);
                        if (stream != null)
                        {
                            Console.WriteLine($"    ? Stream accessible, length: {stream.Length} bytes");
                        }
                        else
                        {
                            Console.WriteLine($"    ? Stream is null");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    ? Error accessing stream: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing embedded resources: {ex.Message}");
            }
        }
    }
}