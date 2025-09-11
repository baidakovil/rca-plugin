using System;
using System.IO;
using System.Text;
using System.IO.Pipes;
using System.Text.Json;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Linq;

namespace Rca.TestAdapter
{
    /// <summary>
    /// Helper class for initializing Revit for testing.
    /// </summary>
    internal static class RevitTestInitializer
    {
        /// <summary>
        /// Ensures that Revit is running and the RCA plugin is initialized.
        /// </summary>
        /// <returns>True if successful, false otherwise.</returns>
        public static bool EnsureRevitIsInitialized()
        {
            try
            {
                if (!IsRevitRunning())
                {
                    Console.WriteLine("ERROR: Revit is not running.");
                    Console.WriteLine("Please start Autodesk Revit and click 'Initialize' in the RCA ribbon tab.");
                    return false;
                }
                
                if (!CheckPipeServerResponsive())
                {
                    Console.WriteLine("ERROR: RCA pipe server not responsive.");
                    Console.WriteLine("Please click the 'Initialize' button in the RCA ribbon tab in Revit.");
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing Revit for testing: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Checks if Revit is running.
        /// </summary>
        /// <returns>True if Revit is running, false otherwise.</returns>
        private static bool IsRevitRunning()
        {
            var possibleProcessNames = new[] 
            { 
                "Revit", "RevitAccelerator", "RevitPreview", 
                "Autodesk.Revit.UI", "Autodesk Revit"
            };
            
            // Check by process name
            foreach (var processName in possibleProcessNames)
            {
                try
                {
                    var processes = Process.GetProcessesByName(processName);
                    if (processes.Length > 0)
                    {
                        Console.WriteLine($"DEBUG: Found {processes.Length} Revit process(es)");
                        return true;
                    }
                }
                catch
                {
                    // Continue checking other process names
                }
            }
            
            // Check by window title containing "Revit"
            try
            {
                var revitProcesses = Process.GetProcesses().Where(p => {
                    try
                    {
                        return !string.IsNullOrEmpty(p.MainWindowTitle) && 
                               p.MainWindowTitle.IndexOf("Revit", StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    catch
                    {
                        return false;
                    }
                }).ToList();
                
                if (revitProcesses.Count > 0)
                {
                    Console.WriteLine($"DEBUG: Found {revitProcesses.Count} potential Revit process(es) by window title");
                    return true;
                }
            }
            catch
            {
                // Ignore errors when searching by window title
            }
            
            Console.WriteLine("DEBUG: No Revit processes found");
            return false;
        }
        
        /// <summary>
        /// Checks if the pipe server is responsive.
        /// </summary>
        /// <returns>True if responsive, false otherwise.</returns>
        private static bool CheckPipeServerResponsive()
        {
            NamedPipeClientStream? pipeClient = null;
            StreamWriter? writer = null;
            StreamReader? reader = null;
            
            try
            {
                Console.WriteLine($"DEBUG: Checking if pipe server is responsive: {Constants.PipeName}");
                
                pipeClient = new NamedPipeClientStream(".", Constants.PipeName, PipeDirection.InOut, PipeOptions.None);
                
                // Try to connect with a 5 second timeout
                Console.WriteLine("DEBUG: Connecting to pipe with 5 second timeout");
                pipeClient.Connect(5000);
                
                if (!pipeClient.IsConnected)
                {
                    Console.WriteLine("DEBUG: Failed to connect to pipe server");
                    return false;
                }
                
                Console.WriteLine("DEBUG: Connected to pipe server");
                
                // Create writer and reader
                writer = new StreamWriter(pipeClient) { AutoFlush = true };
                reader = new StreamReader(pipeClient);
                
                // Send a status command
                var command = new PipeCommand { Command = "STATUS", Payload = "TEST_ADAPTER" };
                var json = JsonSerializer.Serialize(command);
                Console.WriteLine($"DEBUG: Sending STATUS command: {json}");
                
                // Write the command
                writer.WriteLine(json);
                
                // Read the response
                var responseJson = reader.ReadLine();
                Console.WriteLine($"DEBUG: Received response: {responseJson ?? "NULL"}");
                
                if (string.IsNullOrEmpty(responseJson))
                {
                    Console.WriteLine("DEBUG: Empty response received");
                    return false;
                }
                
                var response = JsonSerializer.Deserialize<PipeResponse>(responseJson);
                var success = response != null && !string.IsNullOrEmpty(response.Status);
                Console.WriteLine($"DEBUG: Response status: {response?.Status ?? "NULL"}, Success: {success}");
                
                // Explicitly close the writer and reader to avoid disposal issues
                Console.WriteLine("DEBUG: Closing writer and reader");
                writer.Close();
                reader.Close();
                
                return success;
            }
            catch (TimeoutException)
            {
                Console.WriteLine("WARNING: Could not connect to RCA pipe server.");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Error checking pipe server: {ex.Message}");
                return false;
            }
            finally
            {
                // Clean up resources in the proper order
                try
                {
                    writer?.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"DEBUG: Error disposing writer: {ex.Message}");
                }
                
                try
                {
                    reader?.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"DEBUG: Error disposing reader: {ex.Message}");
                }
                
                try
                {
                    if (pipeClient != null)
                    {
                        if (pipeClient.IsConnected)
                        {
                            pipeClient.Close();
                        }
                        pipeClient.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"DEBUG: Error disposing pipe client: {ex.Message}");
                }
            }
        }
    }
}