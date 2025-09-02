using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;

namespace RcaReloadTrigger
{
    /// <summary>
    /// Command-line utility for sending reload commands to the RCA Loader via named pipes.
    /// </summary>
    public class Program
    {
        private const string DefaultPipeName = "RcaPluginReloader";

        /// <summary>
        /// Main entry point for the reload trigger utility.
        /// </summary>
        public static async Task<int> Main(string[] args)
        {
            try
            {
                var pipeName = DefaultPipeName;
                var command = "RELOAD";
                string assemblyPath = null;

                // Parse command line arguments
                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i].ToLowerInvariant())
                    {
                        case "--pipe":
                        case "-p":
                            if (i + 1 < args.Length)
                                pipeName = args[++i];
                            break;
                        case "--assembly":
                        case "-a":
                            if (i + 1 < args.Length)
                                assemblyPath = args[++i];
                            break;
                        case "--help":
                        case "-h":
                            ShowHelp();
                            return 0;
                        case "ping":
                            command = "PING";
                            break;
                        case "status":
                            command = "STATUS";
                            break;
                        case "reload":
                            command = "RELOAD";
                            break;
                    }
                }

                // Send command
                var success = await SendCommand(pipeName, command, assemblyPath);
                return success ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Sends a command to the named pipe server.
        /// </summary>
        private static async Task<bool> SendCommand(string pipeName, string command, string assemblyPath)
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut))
                {
                    Console.WriteLine($"Connecting to pipe '{pipeName}'...");
                    
                    // Connect with timeout
                    await client.ConnectAsync(5000);
                    
                    if (!client.IsConnected)
                    {
                        Console.WriteLine("Failed to connect to the pipe server.");
                        return false;
                    }

                    Console.WriteLine("Connected!");

                    using (var writer = new StreamWriter(client, Encoding.UTF8, 1024, true))
                    using (var reader = new StreamReader(client, Encoding.UTF8, false, 1024, true))
                    {
                        // Send command
                        var message = string.IsNullOrEmpty(assemblyPath) ? command : $"{command}|{assemblyPath}";
                        Console.WriteLine($"Sending: {message}");
                        
                        await writer.WriteLineAsync(message);
                        await writer.FlushAsync();

                        // Read response
                        var response = await reader.ReadLineAsync();
                        Console.WriteLine($"Response: {response}");

                        return response?.StartsWith("OK") == true;
                    }
                }
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Connection timeout. Make sure the RCA Loader is running in Revit.");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Communication error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Shows help information.
        /// </summary>
        private static void ShowHelp()
        {
            Console.WriteLine("RCA Plugin Reload Trigger");
            Console.WriteLine();
            Console.WriteLine("Usage: RcaReloadTrigger [command] [options]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  reload    Reload the plugin (default)");
            Console.WriteLine("  ping      Test connection to loader");
            Console.WriteLine("  status    Get loader status");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -p, --pipe <name>       Named pipe name (default: RcaPluginReloader)");
            Console.WriteLine("  -a, --assembly <path>   Path to assembly to reload");
            Console.WriteLine("  -h, --help              Show this help");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  RcaReloadTrigger reload");
            Console.WriteLine("  RcaReloadTrigger ping");
            Console.WriteLine("  RcaReloadTrigger reload --assembly \"C:\\path\\to\\plugin.dll\"");
        }
    }
}