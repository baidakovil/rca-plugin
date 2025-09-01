using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Rca.HotReload.Client
{
    /// <summary>
    /// Simple command-line client for sending hot-reload commands to the RCA Loader.
    /// </summary>
    class Program
    {
        private const string PipeName = "rca.hotreload";
        private const int TimeoutMs = 5000;

        static async Task<int> Main(string[] args)
        {
            try
            {
                var command = ParseArguments(args);
                if (command == null)
                {
                    ShowUsage();
                    return 1;
                }

                Console.WriteLine($"Sending command: {command.Command}");
                var response = await SendCommandAsync(command);
                
                if (response != null)
                {
                    Console.WriteLine($"Response: {response}");
                    return 0;
                }
                else
                {
                    Console.WriteLine("Failed to receive response");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        private static HotReloadCommand ParseArguments(string[] args)
        {
            if (args.Length == 0)
            {
                return new HotReloadCommand { Command = "PING" };
            }

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "--command":
                    case "-c":
                        if (i + 1 < args.Length)
                        {
                            return new HotReloadCommand { Command = args[i + 1].ToUpperInvariant() };
                        }
                        break;

                    case "--test":
                    case "-t":
                        if (i + 1 < args.Length)
                        {
                            return new HotReloadCommand { Command = "RUN_TEST", Filter = args[i + 1] };
                        }
                        break;

                    case "--help":
                    case "-h":
                        return null;
                }
            }

            // Default to PING if no valid command found
            return new HotReloadCommand { Command = "PING" };
        }

        private static async Task<string> SendCommandAsync(HotReloadCommand command)
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
                {
                    Console.WriteLine("Connecting to RCA hot-reload server...");
                    await client.ConnectAsync(TimeoutMs);
                    
                    using (var writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: true))
                    using (var reader = new StreamReader(client, Encoding.UTF8, leaveOpen: true))
                    {
                        writer.AutoFlush = true;

                        // Read initial status
                        var initialResponse = await reader.ReadLineAsync();
                        Console.WriteLine($"Connected: {initialResponse}");

                        // Send command
                        var commandJson = JsonSerializer.Serialize(command);
                        await writer.WriteLineAsync(commandJson);

                        // Read response
                        var response = await reader.ReadLineAsync();
                        return response;
                    }
                }
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Connection timeout - make sure RCA Loader is running in Revit");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection failed: {ex.Message}");
                return null;
            }
        }

        private static void ShowUsage()
        {
            Console.WriteLine("RCA Hot Reload Client");
            Console.WriteLine("Usage:");
            Console.WriteLine("  Rca.HotReload.Client [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --command, -c <COMMAND>  Send command (PING, RELOAD, STATUS)");
            Console.WriteLine("  --test, -t <FILTER>      Run test with filter");
            Console.WriteLine("  --help, -h               Show this help");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  Rca.HotReload.Client                    # Send PING");
            Console.WriteLine("  Rca.HotReload.Client -c RELOAD          # Trigger reload");
            Console.WriteLine("  Rca.HotReload.Client -c STATUS          # Get status");
            Console.WriteLine("  Rca.HotReload.Client -t MyTest.Method   # Run specific test");
        }
    }

    public class HotReloadCommand
    {
        public string Command { get; set; }
        public string Filter { get; set; }
    }
}