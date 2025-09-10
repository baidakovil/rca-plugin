using System;
using System.Reflection;

namespace ResourceTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Testing Embedded Resources");
            Console.WriteLine("==========================");
            
            var asm = Assembly.GetExecutingAssembly();
            var resources = asm.GetManifestResourceNames();
            
            Console.WriteLine($"Assembly: {asm.GetName().Name}");
            Console.WriteLine($"Location: {asm.Location}");
            Console.WriteLine($"Total embedded resources: {resources.Length}");
            Console.WriteLine();
            
            if (resources.Length == 0)
            {
                Console.WriteLine("? NO EMBEDDED RESOURCES FOUND!");
                Console.WriteLine("This means the files are not being included in the build.");
            }
            else
            {
                Console.WriteLine("Found resources:");
                foreach (var resource in resources)
                {
                    Console.WriteLine($"  • {resource}");
                    
                    // Test if we can actually open the resource
                    try
                    {
                        using var stream = asm.GetManifestResourceStream(resource);
                        if (stream != null)
                        {
                            Console.WriteLine($"    ? Accessible ({stream.Length} bytes)");
                        }
                        else
                        {
                            Console.WriteLine($"    ? Stream is null");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    ? Error: {ex.Message}");
                    }
                }
            }
            
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}